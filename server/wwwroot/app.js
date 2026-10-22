/* 9mRoom dev console — 只用于验证协议，不是正式客户端渲染器。 */

const APPS = [
  { id: 'vscode', category: 'coding', template: '💻在 {app} 里搬砖' },
  { id: 'chrome', category: 'browsing', template: '🌐用 {app} 查资料' },
  { id: 'bilibili', category: 'media', template: '📺在 {app} 摸鱼' },
  { id: 'steam', category: 'game', template: '🎮在 {app} 里坐牢' },
  { id: 'wechat', category: 'other', template: '💬在 {app} 回消息' },
  { id: '9mroom-console', category: 'other', template: '🛠 {app} 调试中' },
];

const TRACKS = [
  { title: '晴天', artist: '周杰伦', duration: 269 },
  { title: 'Alegro?', artist: '紫月菜乃', duration: 214 },
  { title: '逃亡', artist: 'ヨルシカ', duration: 288 },
  { title: 'Nocturnal Badgirls', artist: 'Rolling Contact', duration: 214 },
  { title: '群青', artist: 'YOASOBI', duration: 245 },
];

const NAMES = ['林墨', '陈屿', '苏晚', '阿岚', '青禾', '小满', '江野', '迟夏'];

const CATALOG = {
  ...Object.fromEntries(APPS.map((app) => [app.id, app.id])),
  vscode: 'VS Code',
  chrome: 'Chrome',
  bilibili: '哔哩哔哩',
  steam: 'Steam',
  wechat: '微信',
  '9mroom-console': '9mRoom 调试台',
};

const SESSION_START = Date.now();
const els = {
  conn: document.getElementById('conn'),
  count: document.getElementById('count'),
  clock: document.getElementById('clock'),
  members: document.getElementById('members'),
  empty: document.getElementById('empty'),
  log: document.getElementById('log'),
  room: document.getElementById('room'),
  name: document.getElementById('name'),
  connect: document.getElementById('connect'),
  addFakes: document.getElementById('add-fakes'),
  clearFakes: document.getElementById('clear-fakes'),
  clearLog: document.getElementById('clear-log'),
};

const members = new Map();
const fakes = [];
let viewer = null;
let heartbeat = null;

/* ---------- 工具 ---------- */

const pick = (arr) => arr[Math.floor(Math.random() * arr.length)];

function currentRoom() {
  return (els.room.value.trim() || 'demo').slice(0, 32);
}

function wsUrl(role, name, device) {
  const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
  const query = new URLSearchParams({
    room: currentRoom(),
    name: name || '匿名',
    device: device || 'windows',
    role,
  });
  return `${proto}//${location.host}/v1/ws?${query}`;
}

function send(ws, type, payload) {
  if (!ws || ws.readyState !== WebSocket.OPEN) return;
  ws.send(JSON.stringify({
    v: 1,
    type,
    id: crypto.randomUUID(),
    ts: Date.now(),
    payload,
  }));
}

function log(text) {
  const li = document.createElement('li');
  const time = document.createElement('time');
  time.textContent = new Date().toLocaleTimeString('zh-CN', { hour12: false });
  li.append(time, document.createTextNode(text));
  els.log.prepend(li);
  while (els.log.children.length > 80) els.log.lastElementChild.remove();
}

function setConn(state, text) {
  els.conn.dataset.state = state;
  els.conn.textContent = text;
}

/* ---------- 渲染（仅调试台） ---------- */

function fmtClock(ms) {
  const total = Math.max(0, Math.floor(ms / 1000));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  return h > 0
    ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
    : `${m}:${String(s).padStart(2, '0')}`;
}

function fmtUptime(ms) {
  const total = Math.max(0, Math.floor(ms / 1000));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  if (h > 0) return `${h}小时${m}分${s}秒`;
  if (m > 0) return `${m}分${s}秒`;
  return `${s}秒`;
}

function renderBlock(block, now) {
  if (!block || !block.data) return '';
  const d = block.data;
  let values;

  switch (block.type) {
    case 'app': {
      if (!d.appId) return '';
      const idle = d.idleSeconds || 0;
      values = {
        app: CATALOG[d.appId] || d.appId,
        category: d.category || '',
        title: d.title || '',
        idle: idle >= 60 ? `空闲${Math.floor(idle / 60)}分` : '',
      };
      break;
    }
    case 'media': {
      if (!d.title) return '';
      let progress = '';
      const duration = d.durationMs || 0;
      if (d.isPlaying && duration > 0) {
        const pos = Math.min(duration, (d.positionMs || 0) + Math.max(0, now - (d.sampledAt || now)));
        progress = `[${fmtClock(pos)}/${fmtClock(duration)}]`;
      }
      values = { title: d.title, artist: d.artist || '', progress };
      break;
    }
    case 'todo':
      if (!d.text) return '';
      values = { todo: d.text };
      break;
    case 'timer': {
      if (!d.startedAt) return '';
      const elapsed = fmtClock(now - d.startedAt);
      const remaining = d.mode === 'countdown' && d.durationMinutes
        ? fmtClock(Math.max(0, d.startedAt + d.durationMinutes * 60000 - now))
        : '';
      values = { elapsed, remaining };
      break;
    }
    case 'uptime':
      if (!d.startedAt) return '';
      values = { uptime: fmtUptime(now - d.startedAt) };
      break;
    case 'text':
      if (!d.text) return '';
      values = { text: d.text };
      break;
    default:
      return '';
  }

  let out = String(block.template || '').replace(/\{([a-zA-Z_]+)\}/g, (_, key) => values[key] ?? '');
  out = out.replace(/[\u0000-\u001f]/g, '').replace(/ +/g, ' ').trim();
  return out.length > 80 ? `${out.slice(0, 79)}…` : out;
}

function renderStatus(status, now) {
  if (!status || !Array.isArray(status.blocks)) return '';
  const parts = status.blocks.map((block) => renderBlock(block, now)).filter(Boolean);
  let out = status.layout === 'stacked' ? parts.join('\n') : parts.join(status.separator || '');
  return out.length > 200 ? `${out.slice(0, 199)}…` : out;
}

function hueOf(name) {
  let hash = 0;
  for (const ch of name || '?') hash = (hash * 31 + ch.codePointAt(0)) % 360;
  return hash;
}

function agoText(ms) {
  const seconds = Math.max(0, Math.round(ms / 1000));
  if (seconds < 5) return '刚刚';
  if (seconds < 60) return `${seconds} 秒前`;
  return `${Math.round(seconds / 60)} 分钟前`;
}

function render() {
  const now = Date.now();
  const list = Array.from(members.values()).sort(
    (a, b) => Number(b.online) - Number(a.online) || String(a.name).localeCompare(String(b.name), 'zh-Hans-CN'),
  );

  els.members.replaceChildren();
  for (const member of list) {
    const item = document.createElement('li');
    item.className = 'member' + (member.online ? '' : ' offline');

    const avatar = document.createElement('div');
    avatar.className = 'avatar';
    avatar.style.setProperty('--hue', hueOf(member.name));
    avatar.textContent = String(member.name || '?').slice(0, 1);
    item.append(avatar);

    const body = document.createElement('div');
    body.className = 'body';

    const top = document.createElement('div');
    top.className = 'top';
    const name = document.createElement('span');
    name.className = 'name';
    name.textContent = member.name;
    const badge = document.createElement('span');
    badge.className = 'badge';
    badge.textContent = member.device === 'android' ? 'Android' : 'Windows';
    top.append(name, badge);

    const status = document.createElement('div');
    status.className = 'status';
    status.textContent = member.online ? renderStatus(member.status, now) : '离线';
    body.append(top, status);
    item.append(body);

    const side = document.createElement('div');
    side.className = 'side';
    const dot = document.createElement('span');
    dot.className = 'dot';
    const ago = document.createElement('span');
    ago.textContent = member.online ? agoText(now - (member.updatedAt || now)) : '';
    side.append(dot, ago);
    item.append(side);

    els.members.append(item);
  }

  const online = list.filter((member) => member.online).length;
  els.count.textContent = `${online} 人在线 / ${list.length} 人`;
  els.empty.hidden = list.length > 0;
}

/* ---------- 观察者连接 ---------- */

function connect() {
  if (viewer) {
    viewer.onclose = null;
    viewer.close();
    viewer = null;
  }
  if (heartbeat) clearInterval(heartbeat);

  members.clear();
  render();
  setConn('wait', '连接中');

  const socket = new WebSocket(wsUrl('viewer', els.name.value.trim() || '观察者', 'windows'));
  viewer = socket;

  socket.onopen = () => {
    setConn('on', '已连接');
    log(`加入房间 ${currentRoom()}`);
    send(socket, 'presence.update', {
      mode: 'focusing',
      status: {
        layout: 'inline',
        separator: ' | ',
        blocks: [
          { id: 'v1', type: 'app', template: '🛠 {app}', data: { appId: '9mroom-console', category: 'other' } },
          { id: 'v2', type: 'uptime', template: '🔌已开机 {uptime}', data: { startedAt: SESSION_START } },
        ],
      },
    });

    heartbeat = setInterval(() => {
      send(socket, 'ping', {});
    }, 20000);
  };

  socket.onmessage = (event) => {
    let message;
    try {
      message = JSON.parse(event.data);
    } catch {
      log('收到非 JSON 消息');
      return;
    }

    const payload = message.payload || {};
    switch (message.type) {
      case 'welcome':
        log(`welcome ${payload.userId}`);
        break;
      case 'room.snapshot':
        members.clear();
        for (const member of payload.members || []) members.set(member.id, member);
        render();
        log(`snapshot ${members.size} 人`);
        break;
      case 'presence.changed':
        if (payload.member) {
          members.set(payload.member.id, payload.member);
          render();
        }
        break;
      case 'member.left':
        members.delete(payload.id);
        render();
        log(`离开 ${payload.id}`);
        break;
      case 'error':
        log(`差错 ${payload.code}: ${payload.message}`);
        break;
      default:
        break;
    }
  };

  socket.onclose = () => {
    if (viewer === socket) setConn('off', '已断开');
    log('连接断开');
  };

  socket.onerror = () => log('连接出错');
}

/* ---------- 假用户 ---------- */

function makeFake(index) {
  const name = NAMES[index % NAMES.length] + (index >= NAMES.length ? `·${index}` : '');
  const app = pick(APPS);
  const track = pick(TRACKS);
  const fake = {
    name,
    device: Math.random() < 0.6 ? 'windows' : 'android',
    app,
    track,
    pos: Math.floor(Math.random() * 90000),
    bootAt: Date.now() - Math.floor(Math.random() * 3 * 3600 * 1000),
    focusStart: Date.now() - Math.floor(Math.random() * 18 * 60 * 1000),
    socket: null,
    timer: null,
  };

  const socket = new WebSocket(wsUrl('sim', name, fake.device));
  fake.socket = socket;
  socket.onopen = () => sendFake(fake);
  socket.onclose = () => log(`假用户断开 ${name}`);
  socket.onerror = () => {};

  fake.timer = setInterval(() => {
    if (socket.readyState !== WebSocket.OPEN) return;
    if (Math.random() < 0.15) fake.app = pick(APPS);
    if (Math.random() < 0.05) {
      fake.track = pick(TRACKS);
      fake.pos = 0;
    }
    fake.pos = (fake.pos + 2000) % Math.max(1000, fake.track.duration * 1000);
    if (Date.now() - fake.focusStart > 25 * 60 * 1000) fake.focusStart = Date.now();
    sendFake(fake);
  }, 2000);

  return fake;
}

function sendFake(fake) {
  const now = Date.now();
  send(fake.socket, 'presence.update', {
    mode: 'focusing',
    status: {
      layout: 'inline',
      separator: ' | ',
      blocks: [
        { id: 'a', type: 'app', template: fake.app.template, data: { appId: fake.app.id, category: fake.app.category } },
        {
          id: 'm',
          type: 'media',
          template: '🎶{title} - {artist} {progress}',
          data: {
            title: fake.track.title,
            artist: fake.track.artist,
            isPlaying: true,
            positionMs: fake.pos,
            durationMs: fake.track.duration * 1000,
            sampledAt: now,
          },
        },
        { id: 't', type: 'timer', template: '⏱ {remaining}', data: { mode: 'countdown', startedAt: fake.focusStart, durationMinutes: 25 } },
        { id: 'u', type: 'uptime', template: '🔌已开机 {uptime}', data: { startedAt: fake.bootAt } },
      ],
    },
  });
}

function addFakes(count) {
  for (let i = 0; i < count; i += 1) {
    fakes.push(makeFake(fakes.length));
  }
  log(`添加 ${count} 个假用户`);
}

function clearFakes() {
  for (const fake of fakes.splice(0)) {
    clearInterval(fake.timer);
    fake.socket.close();
  }
  log('清空假用户');
}

/* ---------- 启动 ---------- */

els.connect.addEventListener('click', connect);
els.addFakes.addEventListener('click', () => addFakes(3));
els.clearFakes.addEventListener('click', clearFakes);
els.clearLog.addEventListener('click', () => els.log.replaceChildren());
els.room.addEventListener('keydown', (event) => {
  if (event.key === 'Enter') connect();
});

setInterval(render, 1000);
setInterval(() => {
  els.clock.textContent = new Date().toLocaleTimeString('zh-CN', { hour12: false });
}, 1000);

const params = new URLSearchParams(location.search);
if (params.get('room')) els.room.value = params.get('room');
if (params.get('name')) els.name.value = params.get('name');

connect();

const autoFakes = Number(params.get('fakes') || 0);
if (autoFakes > 0) {
  setTimeout(() => addFakes(autoFakes), 600);
}
