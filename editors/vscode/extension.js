// CsWSC 拡張機能。
// 補完などの解析は cswsc-server.exe (src/CsWSC.LanguageServer) が行い、ここは VS Code との橋渡しをする。
// サーバーとは標準入出力で 1 行 1 JSON をやりとりする。位置はすべて UTF-16 オフセット。
'use strict';

const vscode = require('vscode');
const cp = require('child_process');
const path = require('path');
const fs = require('fs');
const readline = require('readline');

const LANGUAGE = 'cswsc';
/** #r で参照するライブラリ (.cswsl)。通常の C# として解析する (実行はできない) */
const LIBRARY_LANGUAGE = 'cswsl';
/** 解析サーバーで解析するドキュメントか */
const isAnalyzed = doc => doc.languageId === LANGUAGE || doc.languageId === LIBRARY_LANGUAGE;

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
    // 起動に失敗したときは exit が来ないので、error でも終了扱いにして待ち中の要求を返す
    this.proc.on('error', e => this.markExited(`解析サーバーを起動できません: ${e.message}`));
    this.proc.on('exit', code => this.markExited(`解析サーバーが終了しました (code ${code})`));
    this.proc.stdin.on('error', e => this.markExited(`解析サーバーに送信できません: ${e.message}`));
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

  markExited(message) {
    if (this.exited) return;
    this.exited = true;
    log.appendLine(message);
    for (const p of this.pending.values()) p.reject(new Error('server exited'));
    this.pending.clear();
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
  const configured = vscode.workspace.getConfiguration('cswsc').get('serverPath');
  if (configured) return configured;
  return path.join(context.extensionPath, 'server', 'cswsc-server.exe');
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
      item.csDoc = doc;
      return item;
    }));
  },
  async resolveCompletionItem(item, token) {
    const r = server && await server.requestWithToken('resolve', { index: item.csIndex }, token);
    if (r?.description) {
      const [signature, ...rest] = r.description.split(/\r?\n/);
      item.detail = signature;
      if (rest.length) item.documentation = new vscode.MarkdownString(rest.join('\n'));
    }
    // 未インポートの型なら using の追加 (補完を要求したときのドキュメントの位置)
    const doc = r?.edits && item.csDoc;
    if (doc) item.additionalTextEdits = r.edits.map(e =>
      vscode.TextEdit.replace(new vscode.Range(doc.positionAt(e.start), doc.positionAt(e.end)), e.newText));
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

const definitionProvider = {
  async provideDefinition(doc, pos, token) {
    if (!server) return undefined;
    const sourceLink = vscode.workspace.getConfiguration('cswsc').get('sourceLink', true);
    // CsWSC の API や .NET の型はソースのダウンロードで時間がかかることがあるので、ステータスバーに表示する
    const r = await vscode.window.withProgress(
      { location: vscode.ProgressLocation.Window, title: 'CsWSC: 定義を探しています' },
      () => server.requestWithToken('definition', { ...params(doc, pos), sourceLink }, token));
    if (!r) return undefined;
    // path が無ければこのドキュメント、あれば #load 先のファイルや Source Link で取得したソース
    return r.map(d => new vscode.Location(d.path ? vscode.Uri.file(d.path) : doc.uri,
      new vscode.Range(d.startLine, d.startCharacter, d.endLine, d.endCharacter)));
  },
};

// 変更できないときは throw すると、その理由が VS Code に表示される
const renameProvider = {
  async prepareRename(doc, pos, token) {
    const r = server && await server.requestWithToken('prepareRename', params(doc, pos), token);
    if (!r) return undefined;
    if (r.error) throw new Error(r.error);
    return { range: new vscode.Range(doc.positionAt(r.start), doc.positionAt(r.end)), placeholder: r.placeholder };
  },
  async provideRenameEdits(doc, pos, newName, token) {
    const r = server && await server.requestWithToken('rename', { ...params(doc, pos), newName }, token);
    if (!r) return undefined;
    if (r.error) throw new Error(r.error);
    const edit = new vscode.WorkspaceEdit();
    for (const e of r.edits) edit.replace(doc.uri, new vscode.Range(doc.positionAt(e.start), doc.positionAt(e.end)), e.newText);
    return edit;
  },
};

// ---- 参照数 (定義の上に「N 個の参照」を表示。数えるのはこのドキュメント内の参照) ----

/** 参照数の設定 (cswsc.codeLens.*) と、サーバーに渡す定義の種類 */
const CODE_LENS_KINDS = { methods: 'method', properties: 'property', fields: 'field', events: 'event', types: 'type' };
const codeLensChanged = new vscode.EventEmitter();

const codeLensProvider = {
  onDidChangeCodeLenses: codeLensChanged.event,
  async provideCodeLenses(doc, token) {
    const config = vscode.workspace.getConfiguration('cswsc.codeLens', doc);
    if (!config.get('enabled', true)) return [];
    const kinds = Object.entries(CODE_LENS_KINDS).filter(([key]) => config.get(key, true)).map(([, kind]) => kind);
    if (kinds.length === 0) return [];
    const r = server && await server.requestWithToken('referenceLenses', { ...params(doc), kinds }, token);
    if (!r) return undefined;
    return r.map(lens => {
      const range = new vscode.Range(doc.positionAt(lens.start), doc.positionAt(lens.end));
      const title = `${lens.references.length} 個の参照`;
      // 参照が無ければクリックしても何もしない
      if (lens.references.length === 0) return new vscode.CodeLens(range, { title, command: '' });
      const locations = lens.references.map(e =>
        new vscode.Location(doc.uri, new vscode.Range(doc.positionAt(e.start), doc.positionAt(e.end))));
      return new vscode.CodeLens(range, { title, command: 'editor.action.showReferences', arguments: [doc.uri, range.start, locations] });
    });
  },
};

// ---- コードアクション (C# と同じく Roslyn 組み込みのコード修正・リファクタリング) ----

// 一覧では題名だけを受け取り、選ばれてから変更内容を計算する (resolveCodeAction)
const codeActionProvider = {
  async provideCodeActions(doc, range, context, token) {
    const r = server && await server.requestWithToken('codeActions',
      { ...params(doc, range.start), end: doc.offsetAt(range.end) }, token);
    if (!r) return undefined;
    return r
      .filter(a => !context.only || context.only.contains(a.kind === 'quickfix' ? vscode.CodeActionKind.QuickFix : vscode.CodeActionKind.Refactor))
      .map(a => {
        const action = new vscode.CodeAction(a.title, a.kind === 'quickfix' ? vscode.CodeActionKind.QuickFix : vscode.CodeActionKind.Refactor);
        // 修正する診断を結び付けると、問題パネルや波線からも選べる
        if (a.diagnostics) action.diagnostics = context.diagnostics.filter(d => a.diagnostics.includes(doc.offsetAt(d.range.start)));
        action.csIndex = a.index;
        action.csUri = doc.uri;
        return action;
      });
  },
  async resolveCodeAction(action, token) {
    const r = server && await server.requestWithToken('resolveCodeAction', { index: action.csIndex }, token);
    if (!r) return action;
    if (r.error) { vscode.window.showWarningMessage(r.error); return action; }
    const doc = vscode.workspace.textDocuments.find(d => d.uri.toString() === action.csUri.toString());
    if (!doc) return action;
    const edit = new vscode.WorkspaceEdit();
    for (const e of r.edits) edit.replace(doc.uri, new vscode.Range(doc.positionAt(e.start), doc.positionAt(e.end)), e.newText);
    action.edit = edit;
    return action;
  },
};

// ---- 書式設定 (C# と同じく Roslyn で整形。設定は .editorconfig に従う) ----

async function format(doc, range, options, token) {
  const p = params(doc, range ? range.start : undefined);
  const r = server && await server.requestWithToken('format', {
    ...p,
    end: range ? doc.offsetAt(range.end) : -1,
    useTabs: !options.insertSpaces,
    tabSize: options.tabSize,
  }, token);
  if (!r) return undefined;
  return r.map(e => vscode.TextEdit.replace(new vscode.Range(doc.positionAt(e.start), doc.positionAt(e.end)), e.newText));
}

const formattingProvider = {
  provideDocumentFormattingEdits: (doc, options, token) => format(doc, undefined, options, token),
  provideDocumentRangeFormattingEdits: (doc, range, options, token) => format(doc, range, options, token),
};

// ---- 色分け (型・メソッドなどを Roslyn の解析結果で色付け) ----

// 順番はサーバーの LanguageService.SemanticTokenTypes と合わせる
const semanticLegend = new vscode.SemanticTokensLegend(
  ['namespace', 'class', 'struct', 'interface', 'enum', 'enumMember', 'typeParameter', 'method', 'property', 'event', 'parameter', 'variable']);

const semanticTokensProvider = {
  async provideDocumentSemanticTokens(doc, token) {
    const r = server && await server.requestWithToken('semanticTokens', params(doc), token);
    if (!r) return undefined;
    const builder = new vscode.SemanticTokensBuilder(semanticLegend);
    for (let i = 0; i < r.length; i += 3) {
      const start = doc.positionAt(r[i]);
      const end = doc.positionAt(r[i] + r[i + 1]);
      if (start.line !== end.line) continue; // 識別子は複数行にまたがらない
      builder.push(start.line, start.character, end.character - start.character, r[i + 2]);
    }
    return builder.build();
  },
};

// ---- ドキュメントコメント (/// と入力したら <summary> などの雛形に展開) ----

async function expandDocComment(e) {
  if (!isAnalyzed(e.document) || e.contentChanges.length !== 1 || e.contentChanges[0].text !== '/') return;
  const editor = vscode.window.activeTextEditor;
  if (!editor || editor.document !== e.document) return;
  const doc = e.document;
  const pos = e.contentChanges[0].range.start.translate(0, 1);
  const line = doc.lineAt(pos.line);
  if (!/^\s*\/\/\/\s*$/.test(line.text) || !line.text.slice(0, pos.character).endsWith('///')) return;
  const version = doc.version;
  const tags = server && await server.request('docComment', params(doc, pos));
  if (!tags || doc.version !== version || vscode.window.activeTextEditor !== editor) return;
  // スニペットは 2 行目以降を 1 行目のインデントに合わせてくれる
  const escape = s => s.replace(/[$}\\]/g, '\\$&');
  const snippet = new vscode.SnippetString(
    [' <summary>', '\n/// $0', '\n/// </summary>', ...tags.map(t => `\n/// ${escape(t)}`)].join(''));
  editor.insertSnippet(snippet, new vscode.Range(pos, line.range.end));
}

// ---- 診断 (入力が止まってから更新) ----

const timers = new Map();

function scheduleDiagnostics(doc) {
  if (!isAnalyzed(doc)) return;
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

/** 無題のエディターを実行するときに書き出す一時ファイルのフォルダ */
const TEMP_DIR = path.join(require('os').tmpdir(), 'cswsc');

/**
 * 実行するスクリプトを用意し、実行するファイルのパス、再実行の前に呼ぶ更新、実行が終わったら呼ぶ後片付けを返す。
 * 実行できなければ undefined。
 * 保存済みのファイルは保存してそのまま実行する。
 * 無題のエディターは保存せず (Undo の履歴を残すためタブもそのまま)、内容を一時ファイルに書き出して実行する。
 * @returns {Promise<{ doc: vscode.TextDocument, file: string, refresh: () => Promise<void>, end: () => void } | undefined>}
 */
async function activeScript() {
  const editor = vscode.window.activeTextEditor;
  if (!editor || editor.document.languageId !== LANGUAGE) return undefined;
  const doc = editor.document;
  if (!doc.isUntitled) {
    if (doc.isDirty && !(await doc.save())) return undefined;
    return {
      doc, file: doc.uri.fsPath, end: () => { },
      refresh: async () => { if (doc.isDirty) await doc.save(); },
    };
  }
  let file;
  try {
    fs.mkdirSync(TEMP_DIR, { recursive: true });
    // 実行中の別の一時ファイルや、前に異常終了して残ったものを上書きしないよう、空いている名前を探す
    const base = path.basename(doc.uri.path).replace(/[\/:*?"<>|]/g, '_');
    file = path.join(TEMP_DIR, `${base}.csws`);
    for (let i = 2; fs.existsSync(file); i++) file = path.join(TEMP_DIR, `${base} (${i}).csws`);
    fs.writeFileSync(file, doc.getText(), 'utf8');
  } catch (e) {
    vscode.window.showErrorMessage(`一時ファイルを書き出せませんでした: ${e.message}`);
    return undefined;
  }
  const temp = vscode.Uri.file(file);
  let ended = false;
  return {
    doc, file,
    refresh: async () => {
      if (!doc.isClosed) fs.writeFileSync(file, doc.getText(), 'utf8');
    },
    end: () => {
      if (ended) return;
      ended = true;
      cleanUpTemp(temp, doc);
    },
  };
}

/**
 * 無題のエディターをデバッグ実行するとき、デバッガーとのやりとりで一時ファイルと無題のエディターを入れ替える。
 * デバッガーには一時ファイルを実行させつつ、VS Code には無題のエディターを実行しているように見せる。
 * - VS Code → デバッガー: 無題のエディターのブレークポイントを一時ファイルのものとして送る (実行中の追加・削除も届く)
 * - デバッガー → VS Code: 止まった位置などの一時ファイルを無題のエディターとして返す (一時ファイルのタブを開かせない)
 * 拡張機能の API ではメッセージを書き換えられないが、VS Code はトラッカーに渡したオブジェクトをそのまま送受信するので、
 * その場で書き換えれば反映される。
 */
const untitledTrackerFactory = {
  async createDebugAdapterTracker(session) {
    // 再起動 (Ctrl+Shift+F5) でもデバッガーが作り直されてここに来るので、最新の内容で実行し直せるようにする
    const run = runs.get(session.configuration.cswscRunId);
    if (run) await run.restart();
    const { cswscUntitled: untitled, cswscTempFile: tempFile } = session.configuration;
    if (!untitled || !tempFile) return undefined;
    const tempKey = tempFile.toLowerCase();
    const name = path.basename(vscode.Uri.parse(untitled).path);
    /** message 内の source を、toTemp なら無題 → 一時ファイル、そうでなければ一時ファイル → 無題に書き換える */
    const rewrite = (message, toTemp) => {
      const visit = value => {
        if (!value || typeof value !== 'object') return;
        if (Array.isArray(value)) return value.forEach(visit);
        const source = value.source;
        if (source && typeof source === 'object' && typeof source.path === 'string') {
          if (toTemp && source.path === untitled) {
            source.path = tempFile;
            source.name = path.basename(tempFile);
          } else if (!toTemp && source.path.toLowerCase() === tempKey) {
            source.path = untitled;
            source.name = name;
          }
        }
        Object.values(value).forEach(visit);
      };
      visit(message);
    };
    return {
      onWillReceiveMessage: message => rewrite(message, true),
      onDidSendMessage: message => rewrite(message, false),
    };
  },
};

/**
 * 実行が終わった一時ファイルを片付ける。
 * ブレークポイントで止まったときに開いたタブは閉じ (編集していても破棄する。元の内容は無題のエディターにある)、
 * 写したブレークポイントとファイルを消す。
 */
async function cleanUpTemp(temp, source) {
  const key = temp.fsPath.toLowerCase();
  const isTemp = uri => uri.scheme === 'file' && uri.fsPath.toLowerCase() === key;
  try {
    const wasActive = isTemp(vscode.window.activeTextEditor?.document.uri ?? vscode.Uri.parse('x:'));
    for (const tab of vscode.window.tabGroups.all.flatMap(g => g.tabs)) {
      if (!(tab.input instanceof vscode.TabInputText) || !isTemp(tab.input.uri)) continue;
      // 未保存の変更があると閉じるときに確認が出るので、タブを前に出して破棄してから閉じる
      const doc = vscode.workspace.textDocuments.find(d => isTemp(d.uri));
      if (doc?.isDirty) {
        await vscode.window.showTextDocument(doc, { viewColumn: tab.group.viewColumn });
        await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');
      } else {
        await vscode.window.tabGroups.close(tab);
      }
    }
    // 止まった位置を表示していた一時ファイルのタブを閉じたら、元の無題のエディターを前に出す
    if (wasActive && !source.isClosed) await vscode.window.showTextDocument(source);
    const bps = sourceBreakpoints(temp);
    if (bps.length) vscode.debug.removeBreakpoints(bps);
    fs.rmSync(temp.fsPath, { force: true });
  } catch (e) {
    log?.appendLine(`一時ファイルの後片付けに失敗: ${e.message}`);
  }
}

/** @returns {vscode.SourceBreakpoint[]} */
function sourceBreakpoints(uri) {
  const key = uri.toString().toLowerCase();
  return vscode.debug.breakpoints.filter(bp =>
    bp instanceof vscode.SourceBreakpoint && bp.location.uri.toString().toLowerCase() === key);
}

/**
 * 実行中のスクリプト。キーは実行ごとの印 (デバッグ実行では構成の cswscRunId)。
 * @type {Map<string, { doc: vscode.TextDocument, stale: boolean, restart: () => Promise<void> }>}
 */
const runs = new Map();

/** @type {vscode.StatusBarItem | undefined} */
let staleStatus;

/**
 * 実行を登録し、終わったら呼ぶ関数を返す。
 * 実行中にスクリプトを編集したら、実行中のものには反映されないことをステータスバーで知らせる。
 * デバッグの再起動ではデバッガーが作り直されるたびに restart が呼ばれ、最新の内容を書き出し直す。
 */
function beginRun(runId, script, onEnd = () => { }) {
  let launched = false;
  const run = {
    doc: script.doc,
    stale: false,
    restart: async () => {
      // 最初の起動は activeScript() で用意済み
      if (!launched) { launched = true; return; }
      clearTimeout(endTimer);
      try {
        await script.refresh();
      } catch (e) {
        vscode.window.showErrorMessage(`スクリプトを書き出せませんでした: ${e.message}`);
      }
      run.stale = false;
      updateStaleStatus();
    },
  };
  runs.set(runId, run);
  /** @type {NodeJS.Timeout | undefined} */
  let endTimer;
  const end = () => {
    if (runs.get(runId) !== run) return;
    runs.delete(runId);
    updateStaleStatus();
    script.end();
    onEnd();
  };
  return {
    end,
    // デバッグの再起動では、いったん終了してから 0.3 秒ほどで起動し直す (VS Code の restartSession)。
    // すぐ片付けると再起動で一時ファイルが無くなるので、少し待って再起動されなければ片付ける
    endLater: () => { clearTimeout(endTimer); endTimer = setTimeout(end, 3000); },
  };
}

/** 実行中のスクリプトが編集されたら印を付ける */
function onScriptEdited(e) {
  if (!e.contentChanges.length) return;
  let changed = false;
  for (const run of runs.values()) {
    if (run.doc === e.document && !run.stale) changed = run.stale = true;
  }
  if (changed) updateStaleStatus();
}

function updateStaleStatus() {
  const stale = [...runs.values()].some(r => r.stale);
  if (!stale) return staleStatus?.hide();
  if (!staleStatus) {
    staleStatus = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    staleStatus.text = '$(warning) 実行中のスクリプトには反映されません';
    staleStatus.tooltip = '実行を始めてからスクリプトを編集しました。編集した内容は次の実行から反映されます。デバッグ中なら再起動 (Ctrl+Shift+F5) で反映できます。';
    staleStatus.backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground');
  }
  staleStatus.show();
}

async function runScript() {
  const script = await activeScript();
  if (!script) return;
  const exe = await exePath();
  if (!exe) return script.end();
  const { end } = beginRun(`${Date.now()}-${Math.random()}`, script);
  const proc = cp.spawn(exe, [script.file], { detached: true, stdio: 'ignore' });
  proc.on('exit', end);
  proc.on('error', end);
  proc.unref();
}

/** C# 拡張のデバッガー (vsdbg) で CsWSC.exe --debug を起動し、ブレークポイントで止まれるようにする */
async function debugScript() {
  const script = await activeScript();
  if (!script) return;
  const { doc, file } = script;
  if (!vscode.extensions.getExtension(CSHARP_EXTENSION)) {
    script.end();
    const install = 'インストール';
    const answer = await vscode.window.showWarningMessage(
      'デバッグ実行には C# 拡張機能 (ms-dotnettools.csharp) が必要です。', install);
    if (answer === install) vscode.commands.executeCommand('workbench.extensions.installExtension', CSHARP_EXTENSION);
    return;
  }
  const exe = await exePath();
  if (!exe) return script.end();
  // 終了したセッションがこの実行のものか見分ける印
  const runId = `${Date.now()}-${Math.random()}`;
  const onTerminate = vscode.debug.onDidTerminateDebugSession(session => {
    if (session.configuration.cswscRunId === runId) run.endLater();
  });
  const run = beginRun(runId, script, () => onTerminate.dispose());
  const started = await vscode.debug.startDebugging(vscode.workspace.getWorkspaceFolder(doc.uri), {
    type: 'coreclr',
    request: 'launch',
    name: 'CsWSC: デバッグ実行',
    cswscRunId: runId,
    // 無題のエディターなら untitledTrackerFactory が一時ファイルと入れ替える
    ...(doc.isUntitled ? { cswscUntitled: doc.uri.toString(), cswscTempFile: file } : {}),
    program: exe,
    args: ['--debug', file],
    cwd: path.dirname(file),
    console: 'internalConsole',
    // Just My Code を切り、メモリ上で読み込むスクリプトのアセンブリでも止まれるようにする
    justMyCode: false,
    // ファイルの BOM の有無などでチェックサムが合わなくても止まれるようにする
    requireExactSource: false,
    symbolOptions: { searchMicrosoftSymbolServer: false },
  }, {
    // 保存は activeScript() で済ませている。VS Code 側の保存 (debug.saveBeforeStart) を走らせると、
    // 無題のエディターに保存ダイアログが出てしまう
    suppressSaveBeforeStart: true,
  });
  if (!started) run.end();
}

const CSHARP_EXTENSION = 'ms-dotnettools.csharp';

/** CsWSC.exe のパス。未設定またはファイルが無ければ選んでもらう (キャンセルなら undefined) */
async function exePath() {
  let exe = vscode.workspace.getConfiguration('cswsc').get('exePath');
  if (!exe || !fs.existsSync(exe)) {
    const picked = await vscode.window.showOpenDialog({
      title: 'CsWSC.exe を選択', canSelectMany: false, filters: { 'CsWSC': ['exe'] },
    });
    if (!picked || picked.length === 0) return;
    exe = picked[0].fsPath;
    await vscode.workspace.getConfiguration('cswsc').update('exePath', exe, vscode.ConfigurationTarget.Global);
  }
  return exe;
}

// ---- 有効化 ----

function activate(context) {
  log = vscode.window.createOutputChannel('CsWSC');
  diagnostics = vscode.languages.createDiagnosticCollection(LANGUAGE);
  startServer(context);

  const selector = [{ language: LANGUAGE }, { language: LIBRARY_LANGUAGE }];
  context.subscriptions.push(
    log,
    diagnostics,
    vscode.languages.registerCompletionItemProvider(selector, completionProvider, '.'),
    vscode.languages.registerHoverProvider(selector, hoverProvider),
    vscode.languages.registerSignatureHelpProvider(selector, signatureProvider, '(', ','),
    vscode.languages.registerDefinitionProvider(selector, definitionProvider),
    vscode.languages.registerRenameProvider(selector, renameProvider),
    vscode.languages.registerCodeLensProvider(selector, codeLensProvider),
    vscode.languages.registerCodeActionsProvider(selector, codeActionProvider,
      { providedCodeActionKinds: [vscode.CodeActionKind.QuickFix, vscode.CodeActionKind.Refactor] }),
    vscode.languages.registerDocumentFormattingEditProvider(selector, formattingProvider),
    vscode.languages.registerDocumentRangeFormattingEditProvider(selector, formattingProvider),
    vscode.languages.registerDocumentSemanticTokensProvider(selector, semanticTokensProvider, semanticLegend),
    codeLensChanged,
    vscode.workspace.onDidChangeConfiguration(e => {
      if (e.affectsConfiguration('cswsc.codeLens')) codeLensChanged.fire();
    }),
    vscode.workspace.onDidOpenTextDocument(scheduleDiagnostics),
    vscode.workspace.onDidChangeTextDocument(e => {
      scheduleDiagnostics(e.document);
      onScriptEdited(e);
      expandDocComment(e);
    }),
    // ライブラリを保存したら、参照しているスクリプトの診断を更新する (サーバーが保存を検知して読み直す)
    vscode.workspace.onDidSaveTextDocument(doc => {
      if (doc.languageId === LIBRARY_LANGUAGE)
        vscode.workspace.textDocuments.filter(d => d.languageId === LANGUAGE).forEach(scheduleDiagnostics);
    }),
    vscode.workspace.onDidCloseTextDocument(doc => {
      if (!isAnalyzed(doc)) return;
      diagnostics.delete(doc.uri);
      server?.request('close', { uri: doc.uri.toString() });
    }),
    vscode.commands.registerCommand('cswsc.run', runScript),
    vscode.commands.registerCommand('cswsc.debug', debugScript),
    vscode.debug.registerDebugAdapterTrackerFactory('coreclr', untitledTrackerFactory),
    vscode.commands.registerCommand('cswsc.restartServer', () => {
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
