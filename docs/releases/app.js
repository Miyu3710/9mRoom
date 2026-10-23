const platforms = {
  windows: {
    name: 'Windows 客户端',
    kicker: 'WINDOWS RELEASES',
    description: 'MSI 安装包与便携版。未签名构建可能触发 SmartScreen 提示。',
    prefix: 'windows-v'
  },
  server: {
    name: 'Linux 服务端',
    kicker: 'SERVER RELEASES',
    description: '自包含 Linux 包；Docker 部署可直接使用仓库中的 compose。',
    prefix: 'server-v'
  },
  android: {
    name: 'Android 客户端',
    kicker: 'ANDROID RELEASES',
    description: 'APK 直接安装；覆盖安装通常可以保留本机配置。',
    prefix: 'android-v'
  }
};

const params = new URLSearchParams(window.location.search);
const platformKey = (params.get('platform') || '').toLowerCase();
const platform = platforms[platformKey] || platforms.windows;

document.title = `9mRoom · ${platform.name}版本历史`;
document.getElementById('platformKicker').textContent = platform.kicker;
document.getElementById('platformTitle').textContent = platform.name;
document.getElementById('platformDescription').textContent = platform.description;

const list = document.getElementById('releaseList');

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function renderInline(value) {
  let html = escapeHtml(value);
  html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
  html = html.replace(/\[([^\]]+)\]\((https?:\/\/[^)\s]+)\)/g, '<a href="$2" target="_blank" rel="noreferrer">$1</a>');
  return html;
}

function renderBody(markdown) {
  const lines = String(markdown || '').replace(/\r\n/g, '\n').split('\n');
  if (lines.length && /^#\s+/.test(lines[0].trim())) {
    lines.shift();
  }

  const html = [];
  let inList = false;
  let inCode = false;

  const closeList = () => {
    if (inList) {
      html.push('</ul>');
      inList = false;
    }
  };

  for (const rawLine of lines) {
    const line = rawLine.trim();

    if (line.startsWith('```')) {
      closeList();
      if (inCode) {
        html.push('</code></pre>');
      } else {
        html.push('<pre><code>');
      }
      inCode = !inCode;
      continue;
    }

    if (inCode) {
      html.push(escapeHtml(rawLine) + '\n');
      continue;
    }

    if (!line) {
      closeList();
      continue;
    }

    const heading = line.match(/^(#{2,3})\s+(.*)$/);
    if (heading) {
      closeList();
      const level = heading[1].length;
      html.push(`<h${level}>${renderInline(heading[2])}</h${level}>`);
      continue;
    }

    const item = line.match(/^[-*]\s+(.*)$/);
    if (item) {
      if (!inList) {
        html.push('<ul>');
        inList = true;
      }
      html.push(`<li>${renderInline(item[1])}</li>`);
      continue;
    }

    closeList();
    html.push(`<p>${renderInline(line)}</p>`);
  }

  closeList();
  if (inCode) {
    html.push('</code></pre>');
  }
  return html.join('');
}

function formatBytes(bytes) {
  if (!Number.isFinite(bytes) || bytes <= 0) return '';
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${value.toFixed(unit === 0 ? 0 : 1)} ${units[unit]}`;
}

function formatDate(value) {
  try {
    return new Intl.DateTimeFormat('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit'
    }).format(new Date(value));
  } catch {
    return '';
  }
}

function renderRelease(release, latest) {
  const card = document.createElement('article');
  card.className = 'release-card';

  const top = document.createElement('div');
  top.className = 'release-top';

  const title = document.createElement('h2');
  title.textContent = release.name || release.tag;
  top.appendChild(title);

  const tag = document.createElement('span');
  tag.className = 'release-tag';
  tag.textContent = release.tag;
  top.appendChild(tag);

  if (latest) {
    const badge = document.createElement('span');
    badge.className = 'latest-badge';
    badge.textContent = '最新';
    top.appendChild(badge);
  }

  const date = document.createElement('span');
  date.className = 'release-date';
  date.textContent = formatDate(release.publishedAt);
  top.appendChild(date);

  card.appendChild(top);

  if (release.assets?.length) {
    const assets = document.createElement('div');
    assets.className = 'asset-list';
    release.assets
      .filter((asset) => asset.name && asset.url)
      .sort((a, b) => {
        const aHash = a.name.endsWith('.sha256') || a.name.endsWith('.txt') || a.name.endsWith('.json');
        const bHash = b.name.endsWith('.sha256') || b.name.endsWith('.txt') || b.name.endsWith('.json');
        return Number(aHash) - Number(bHash);
      })
      .forEach((asset) => {
        const link = document.createElement('a');
        link.className = 'asset';
        link.href = asset.url;
        link.target = '_blank';
        link.rel = 'noreferrer';
        link.textContent = asset.name;
        if (asset.size) {
          const size = document.createElement('span');
          size.textContent = formatBytes(asset.size);
          link.appendChild(size);
        }
        assets.appendChild(link);
      });
    card.appendChild(assets);
  }

  if (release.body) {
    const details = document.createElement('details');
    details.className = 'release-notes';

    const summary = document.createElement('summary');
    summary.textContent = '更新说明';
    details.appendChild(summary);

    const body = document.createElement('div');
    body.className = 'release-body';
    body.innerHTML = renderBody(release.body);
    details.appendChild(body);
    card.appendChild(details);
  }

  return card;
}

async function loadReleases() {
  try {
    const response = await fetch('./releases.json', { cache: 'no-store' });
    if (!response.ok) {
      throw new Error(`发布数据返回 ${response.status}`);
    }

    const releases = await response.json();
    const filtered = releases
      .filter((release) => release.platform === platformKey)
      .sort((a, b) => new Date(b.publishedAt) - new Date(a.publishedAt));

    list.replaceChildren();
    if (!filtered.length) {
      const empty = document.createElement('div');
      empty.className = 'state-card';
      empty.textContent = '这个端还没有发布版本。';
      list.appendChild(empty);
      return;
    }

    filtered.forEach((release, index) => list.appendChild(renderRelease(release, index === 0)));
  } catch (error) {
    list.replaceChildren();
    const card = document.createElement('div');
    card.className = 'state-card error';
    card.textContent = `读取版本历史失败：${error.message || '网络错误'}`;
    list.appendChild(card);
  }
}

loadReleases();
