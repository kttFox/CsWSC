// CsWSC 拡張機能。
// 補完などの解析は csws-server.exe (src/CsWSC.LanguageServer) が行い、ここは VS Code との橋渡しをする。
// サーバーとは標準入出力で 1 行 1 JSON をやりとりする。位置はすべて UTF-16 オフセット。
'use strict';

const vscode = require('vscode');
const cp = require('child_process');
const path = require('path');
const fs = require('fs');
const readline = require('readline');

const LANGUAGE = 'csws';

/** @type {Server | undefined} */
let server;
/** @type {vscode.DiagnosticCollection} */
let diagnostics;
/** @type {vscode.OutputChannel} */
let log;

class Server {
  constructor(exe) {
    this.nextId = 1;
    /** @type {Map<number, {resolve: Function, reject: Function}>} */
    this.pending = new Map();
    this.proc = cp.spawn(exe, [], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });
    this.proc.on('error', e => log.appendLine(`解析サーバーを起動できません: ${e.message}`));
    this.proc.on('exit', code => {
      log.appendLine(`解析サーバーが終了しました (code ${code})`);
      for (const p of this.pending.values()) p.reject(new Error('server exited'));
      this.pending.clear();
      this.exited = true;
    });
    this.proc.stderr.on('data', d => log.append(d.toString()));
    readline.createInterface({ input: this.proc.stdout }).on('line', line => {
      let res;
      try { res = JSON.parse(line); } catch { return; }
      const p = this.pending.get(res.id);
      if (!p) return;
      this.pending.delete(res.id);
      if (res.error) { log.appendLine(`エラー: ${res.error}`); p.resolve(null); }
      else p.resolve(res.result);
    });
  }

  /** @returns {Promise<any>} */
  request(method, params) {
    if (this.exited) return Promise.resolve(null);
    const id = this.nextId++;
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.proc.stdin.write(JSON.stringify({ id, method, ...params }) + '\n');
    });
  }

  /** 古い要求 (入力中に追い越されたもの) の結果は使わないよう、キャンセル時は null にする */
  async requestWithToken(method, params, token) {
    const result = await this.request(method, params);
    return token && token.isCancellationRequested ? null : result;
  }

  dispose() {
    if (this.exited) return;
    try { this.proc.stdin.write(JSON.stringify({ id: 0, method: 'shutdown' }) + '\n'); } catch { /* 既に終了 */ }
    setTimeout(() => { if (!this.exited) this.proc.kill(); }, 1000);
  }
}

function serverPath(context) {
  const configured = vscode.workspace.getConfiguration('csws').get('serverPath');
  if (configured) return configured;
  return path.join(context.extensionPath, 'server', 'csws-server.exe');
}

function startServer(context) {
  const exe = serverPath(context);
  if (!fs.existsSync(exe)) {
    vscode.window.showErrorMessage(`CsWSC 解析サーバーが見つかりません: ${exe}`);
    return;
  }
  server = new Server(exe);
  log.appendLine(`解析サーバーを起動しました: ${exe}`);
}

const params = (doc, pos) => ({ uri: doc.uri.toString(), text: doc.getText(), offset: pos === undefined ? 0 : doc.offsetAt(pos) });

const KINDS = {
  method: vscode.CompletionItemKind.Method, property: vscode.CompletionItemKind.Property,
  field: vscode.CompletionItemKind.Field, event: vscode.CompletionItemKind.Event,
  class: vscode.CompletionItemKind.Class, struct: vscode.CompletionItemKind.Struct,
  interface: vscode.CompletionItemKind.Interface, enum: vscode.CompletionItemKind.Enum,
  enumMember: vscode.CompletionItemKind.EnumMember, module: vscode.CompletionItemKind.Module,
  keyword: vscode.CompletionItemKind.Keyword, variable: vscode.CompletionItemKind.Variable,
  constant: vscode.CompletionItemKind.Constant, typeParameter: vscode.CompletionItemKind.TypeParameter,
  snippet: vscode.CompletionItemKind.Snippet, text: vscode.CompletionItemKind.Text,
};

const completionProvider = {
  async provideCompletionItems(doc, pos, token) {
    const r = server && await server.requestWithToken('completion', params(doc, pos), token);
    if (!r) return undefined;
    const range = new vscode.Range(doc.positionAt(r.start), doc.positionAt(r.end));
    return new vscode.CompletionList(r.items.map(i => {
      const item = new vscode.CompletionItem({ label: i.label, description: i.detail || undefined }, KINDS[i.kind]);
      item.insertText = i.insertText;
      item.sortText = i.sortText || undefined;
      item.filterText = i.filterText || undefined;
      item.range = range;
      item.csIndex = i.index;
      return item;
    }));
  },
  async resolveCompletionItem(item, token) {
    const text = server && await server.requestWithToken('resolve', { index: item.csIndex }, token);
    if (text) {
      const [signature, ...rest] = text.split(/\r?\n/);
      item.detail = signature;
      if (rest.length) item.documentation = new vscode.MarkdownString(rest.join('\n'));
    }
    return item;
  },
};

const hoverProvider = {
  async provideHover(doc, pos, token) {
    const r = server && await server.requestWithToken('hover', params(doc, pos), token);
    if (!r) return undefined;
    const md = new vscode.MarkdownString();
    md.appendCodeblock(r.signature, 'csharp');
    if (r.documentation) md.appendMarkdown(r.documentation);
    return new vscode.Hover(md, new vscode.Range(doc.positionAt(r.start), doc.positionAt(r.end)));
  },
};

const signatureProvider = {
  async provideSignatureHelp(doc, pos, token) {
    const r = server && await server.requestWithToken('signature', params(doc, pos), token);
    if (!r) return undefined;
    const help = new vscode.SignatureHelp();
    help.signatures = r.signatures.map(s => {
      const sig = new vscode.SignatureInformation(s.label, s.documentation ? new vscode.MarkdownString(s.documentation) : undefined);
      sig.parameters = s.parameters.map(p => new vscode.ParameterInformation(p.label, p.documentation || undefined));
      return sig;
    });
    help.activeSignature = r.activeSignature;
    help.activeParameter = r.activeParameter;
    return help;
  },
};

// ---- 診断 (入力が止まってから更新) ----

const timers = new Map();

function scheduleDiagnostics(doc) {
  if (doc.languageId !== LANGUAGE) return;
  const key = doc.uri.toString();
  clearTimeout(timers.get(key));
  timers.set(key, setTimeout(() => updateDiagnostics(doc), 500));
}

async function updateDiagnostics(doc) {
  if (!server || doc.isClosed) return;
  const version = doc.version;
  const r = await server.request('diagnostics', params(doc));
  if (!r || doc.isClosed || doc.version !== version) return;
  diagnostics.set(doc.uri, r.map(d => {
    const diag = new vscode.Diagnostic(new vscode.Range(doc.positionAt(d.start), doc.positionAt(d.end)), d.message,
      d.severity === 'error' ? vscode.DiagnosticSeverity.Error : vscode.DiagnosticSeverity.Warning);
    diag.code = d.code;
    diag.source = 'CsWSC';
    return diag;
  }));
}

// ---- 実行 ----

async function runScript() {
  const editor = vscode.window.activeTextEditor;
  if (!editor || editor.document.languageId !== LANGUAGE) return;
  const doc = editor.document;
  if (doc.isUntitled) {
    vscode.window.showWarningMessage('先にスクリプトを保存してください。');
    return;
  }
  if (doc.isDirty && !(await doc.save())) return;

  let exe = vscode.workspace.getConfiguration('csws').get('exePath');
  if (!exe || !fs.existsSync(exe)) {
    const picked = await vscode.window.showOpenDialog({
      title: 'CsWSC.exe を選択', canSelectMany: false, filters: { 'CsWSC': ['exe'] },
    });
    if (!picked || picked.length === 0) return;
    exe = picked[0].fsPath;
    await vscode.workspace.getConfiguration('csws').update('exePath', exe, vscode.ConfigurationTarget.Global);
  }
  cp.spawn(exe, [doc.uri.fsPath], { detached: true, stdio: 'ignore' }).unref();
}

// ---- 有効化 ----

function activate(context) {
  log = vscode.window.createOutputChannel('CsWSC');
  diagnostics = vscode.languages.createDiagnosticCollection(LANGUAGE);
  startServer(context);

  const selector = { language: LANGUAGE };
  context.subscriptions.push(
    log,
    diagnostics,
    vscode.languages.registerCompletionItemProvider(selector, completionProvider, '.'),
    vscode.languages.registerHoverProvider(selector, hoverProvider),
    vscode.languages.registerSignatureHelpProvider(selector, signatureProvider, '(', ','),
    vscode.workspace.onDidOpenTextDocument(scheduleDiagnostics),
    vscode.workspace.onDidChangeTextDocument(e => scheduleDiagnostics(e.document)),
    vscode.workspace.onDidCloseTextDocument(doc => {
      if (doc.languageId !== LANGUAGE) return;
      diagnostics.delete(doc.uri);
      server?.request('close', { uri: doc.uri.toString() });
    }),
    vscode.commands.registerCommand('csws.run', runScript),
    vscode.commands.registerCommand('csws.restartServer', () => {
      server?.dispose();
      startServer(context);
      vscode.workspace.textDocuments.forEach(scheduleDiagnostics);
    }),
    { dispose: () => server?.dispose() },
  );
  vscode.workspace.textDocuments.forEach(scheduleDiagnostics);
}

function deactivate() {
  server?.dispose();
}

module.exports = { activate, deactivate };
