const urlInput = document.getElementById('urlInput');
const downloadBtn = document.getElementById('downloadBtn');
const statusBadge = document.getElementById('statusBadge');
const statusText = document.getElementById('statusText');
const linkList = document.getElementById('linkList');
const linkCount = document.getElementById('linkCount');
const batchBar = document.getElementById('batchBar');
const downloadAllBtn = document.getElementById('downloadAllBtn');
const downloadFilteredBtn = document.getElementById('downloadFilteredBtn');
const clearHistoryBtn = document.getElementById('clearHistoryBtn');
const historyList = document.getElementById('historyList');
const filterBar = document.getElementById('filterBar');
const videoInfo = document.getElementById('videoInfo');
const videoTitle = document.getElementById('videoTitle');
const videoDuration = document.getElementById('videoDuration');
const activeDownloads = document.getElementById('activeDownloads');
const activeDownloadCount = document.getElementById('activeDownloadCount');

// YouTube modal
const ytModal = document.getElementById('ytModal');
const ytModalTitle = document.getElementById('ytModalTitle');
const ytModalCancel = document.getElementById('ytModalCancel');
const ytModalDownload = document.getElementById('ytModalDownload');
const resolutionGrid = document.getElementById('resolutionGrid');
const formatToggle = document.getElementById('formatToggle');

let allLinks = [];
let currentFilter = 'all';
let history = JSON.parse(localStorage.getItem('fluxget_history') || '[]');
let pendingYouTubeUrl = null;
let selectedResolution = 720;
let selectedFormat = 'mp4';

// Tab switching
document.querySelectorAll('.tab').forEach(tab => {
  tab.addEventListener('click', () => {
    document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
    document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
    tab.classList.add('active');
    document.getElementById('tab-' + tab.dataset.tab).classList.add('active');
  });
});

// Check server status
async function checkStatus() {
  try {
    const response = await chrome.runtime.sendMessage({ action: 'checkHealth' });
    const connected = response?.running;
    statusBadge.classList.toggle('connected', connected);
    statusText.textContent = connected ? 'Bagli' : 'Baglanti yok';
    downloadBtn.disabled = !connected || !urlInput.value.trim();
  } catch {
    statusBadge.classList.remove('connected');
    statusText.textContent = 'Baglanti yok';
    downloadBtn.disabled = true;
  }
}

// Download URL
async function downloadUrl(url, filename = null, resolution = null, format = null) {
  if (!url) return;

  if (url.startsWith('blob:') || url.startsWith('data:')) {
    showNotification('Bu URL indirilemez', true);
    return;
  }

  try {
    const response = await chrome.runtime.sendMessage({
      action: 'download',
      url: url,
      filename: filename,
      resolution: resolution,
      format: format
    });

    if (response?.success) {
      urlInput.value = '';
      videoInfo.style.display = 'none';
      addToHistory(url, filename || url, 'completed');
      showNotification(resolution ? `${resolution}p indirme baslatildi!` : 'Indirme baslatildi!');
    } else {
      addToHistory(url, filename || url, 'error');
      showNotification(response?.error || 'Indirme baslatilamadi', true);
    }
  } catch (error) {
    showNotification('Hata: ' + error.message, true);
  }
}

// YouTube Modal
function showYouTubeModal(url, title) {
  pendingYouTubeUrl = url;
  ytModalTitle.textContent = title || 'Cozunurluk secin';
  ytModal.classList.add('show');
  
  // Varsayilan secim
  selectedResolution = 720;
  selectedFormat = 'mp4';
  updateResolutionSelection();
  updateFormatSelection();
}

function hideYouTubeModal() {
  ytModal.classList.remove('show');
  pendingYouTubeUrl = null;
}

function updateResolutionSelection() {
  resolutionGrid.querySelectorAll('.resolution-card').forEach(opt => {
    const res = parseInt(opt.dataset.res);
    opt.classList.toggle('selected', res === selectedResolution);
  });
}

function updateFormatSelection() {
  formatToggle.querySelectorAll('.format-option').forEach(opt => {
    opt.classList.toggle('active', opt.dataset.format === selectedFormat);
  });
  
  // MP3 icin cozunurluk gizle
  const resOptions = resolutionGrid.querySelectorAll('.resolution-card');
  resOptions.forEach(opt => {
    if (selectedFormat === 'mp3') {
      opt.style.display = 'none';
    } else {
      opt.style.display = '';
    }
  });
}

// Resolution secimi
resolutionGrid.addEventListener('click', (e) => {
  const option = e.target.closest('.resolution-card');
  if (!option) return;
  selectedResolution = parseInt(option.dataset.res);
  updateResolutionSelection();
});

// Format secimi
formatToggle.addEventListener('click', (e) => {
  const option = e.target.closest('.format-option');
  if (!option) return;
  selectedFormat = option.dataset.format;
  updateFormatSelection();
});

// Modal butonlari
ytModalCancel.addEventListener('click', hideYouTubeModal);

ytModalDownload.addEventListener('click', () => {
  if (!pendingYouTubeUrl) return;
  
  if (selectedFormat === 'mp3') {
    downloadUrl(pendingYouTubeUrl, null, null, 'mp3');
  } else {
    downloadUrl(pendingYouTubeUrl, null, selectedResolution, 'mp4');
  }
  
  hideYouTubeModal();
});

// Modal disina tiklayinca kapat
ytModal.addEventListener('click', (e) => {
  if (e.target === ytModal) hideYouTubeModal();
});

// ESC ile kapat
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape' && ytModal.classList.contains('show')) {
    hideYouTubeModal();
  }
});

// Add to history
function addToHistory(url, filename, status) {
  const item = {
    url,
    filename: filename || extractFilename(url),
    status,
    time: Date.now()
  };
  history.unshift(item);
  if (history.length > 50) history = history.slice(0, 50);
  localStorage.setItem('fluxget_history', JSON.stringify(history));
  renderHistory();
}

function extractFilename(url) {
  try {
    const pathname = new URL(url).pathname;
    return pathname.split('/').pop() || 'download';
  } catch {
    return 'download';
  }
}

function showNotification(message, isError = false) {
  const notification = document.createElement('div');
  notification.className = 'notification ' + (isError ? 'error' : 'success');
  notification.textContent = message;
  document.body.appendChild(notification);
  setTimeout(() => notification.remove(), 2000);
}

// Get detected links from current tab
async function getDetectedLinks() {
  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab?.id) return;

    const response = await chrome.tabs.sendMessage(tab.id, { action: 'getLinks' });
    if (response?.links?.length > 0) {
      allLinks = response.links;
      renderLinks(allLinks);
    } else {
      allLinks = [];
      renderLinks([]);
    }
  } catch {
    allLinks = [];
    linkList.innerHTML = `
      <div class="empty-state">
        <div class="icon">&#128279;</div>
        <div class="text">Bu sayfada link algilanamadi</div>
      </div>`;
    linkCount.textContent = '0';
    batchBar.style.display = 'none';
  }
}

// Render links
function renderLinks(links) {
  const filtered = currentFilter === 'all' ? links : links.filter(l => l.type === currentFilter);

  linkCount.textContent = filtered.length;
  batchBar.style.display = filtered.length > 0 ? 'flex' : 'none';

  if (!filtered.length) {
    linkList.innerHTML = `
      <div class="empty-state">
        <div class="icon">&#128279;</div>
        <div class="text">${currentFilter === 'all' ? 'Bu sayfada link algilanamadi' : 'Bu turde link bulunamadi'}</div>
      </div>`;
    return;
  }

  linkList.innerHTML = filtered.map(link => {
    const isYT = link.isYouTube;
    const iconClass = isYT ? 'video' : link.type;
    const icon = isYT ? '&#127909;' : getFileIcon(link.type);
    const label = isYT ? link.text || 'YouTube Video' : escapeHtml(link.filename);
    const sub = isYT ? 'Cozunurluk secmek icin tiklayin' : escapeHtml(link.url);

    return `
      <div class="link-item" data-url="${escapeHtml(link.url)}" data-type="${iconClass}" data-youtube="${isYT ? '1' : '0'}" data-title="${escapeHtml(link.text || '')}">
        <div class="link-icon ${iconClass}">
          ${icon}
        </div>
        <div class="link-info">
          <div class="link-name">${label}</div>
          <div class="link-url">${sub}</div>
        </div>
        <div class="link-actions">
          <button class="action-btn download-single" title="Indir">&#8595;</button>
        </div>
      </div>
    `;
  }).join('');

  linkList.querySelectorAll('.link-item').forEach(item => {
    const handleClick = () => {
      const isYT = item.dataset.youtube === '1';
      if (isYT) {
        showYouTubeModal(item.dataset.url, item.dataset.title || 'YouTube Video');
      } else {
        downloadUrl(item.dataset.url);
      }
    };

    item.querySelector('.download-single').addEventListener('click', (e) => {
      e.stopPropagation();
      handleClick();
    });

    item.addEventListener('click', handleClick);
  });
}

// Filter chips
filterBar.querySelectorAll('.filter-chip').forEach(chip => {
  chip.addEventListener('click', () => {
    filterBar.querySelectorAll('.filter-chip').forEach(c => c.classList.remove('active'));
    chip.classList.add('active');
    currentFilter = chip.dataset.filter;
    renderLinks(allLinks);
  });
});

// Download all
downloadAllBtn.addEventListener('click', () => {
  allLinks.forEach((link, i) => {
    setTimeout(() => {
      if (link.isYouTube) {
        showYouTubeModal(link.url, link.text);
      } else {
        downloadUrl(link.url, link.filename);
      }
    }, i * 200);
  });
});

// Download filtered
downloadFilteredBtn.addEventListener('click', () => {
  const filtered = currentFilter === 'all' ? allLinks : allLinks.filter(l => l.type === currentFilter);
  filtered.forEach((link, i) => {
    setTimeout(() => {
      if (link.isYouTube) {
        showYouTubeModal(link.url, link.text);
      } else {
        downloadUrl(link.url, link.filename);
      }
    }, i * 200);
  });
});

function getFileIcon(type) {
  const icons = {
    video: '&#127916;',
    audio: '&#127925;',
    image: '&#128444;',
    document: '&#128196;',
    archive: '&#128230;',
    general: '&#128229;'
  };
  return icons[type] || icons.general;
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

function renderHistory() {
  if (!history.length) {
    historyList.innerHTML = `
      <div class="empty-state">
        <div class="icon">&#128216;</div>
        <div class="text">Henuz indirme yok</div>
      </div>`;
    return;
  }

  historyList.innerHTML = history.slice(0, 20).map(item => `
    <div class="history-item">
      <div class="history-status ${item.status}"></div>
      <div class="link-info">
        <div class="link-name">${escapeHtml(item.filename)}</div>
        <div class="link-url">${escapeHtml(item.url)}</div>
      </div>
    </div>
  `).join('');
}

clearHistoryBtn.addEventListener('click', () => {
  history = [];
  localStorage.removeItem('fluxget_history');
  renderHistory();
});

// URL input
urlInput.addEventListener('input', () => {
  downloadBtn.disabled = !urlInput.value.trim() || statusText.textContent !== 'Bagli';

  const url = urlInput.value.trim();
  if (url.includes('youtube.com/watch') || url.includes('youtu.be/') || url.includes('youtube.com/shorts/')) {
    videoInfo.style.display = 'block';
    videoTitle.textContent = 'YouTube Video';
    videoDuration.textContent = 'Cozunurluk secmek icin "Indir" butonuna tiklayin';
  } else {
    videoInfo.style.display = 'none';
  }
});

urlInput.addEventListener('keydown', (e) => {
  if (e.key === 'Enter' && urlInput.value.trim()) {
    const url = urlInput.value.trim();
    if (url.includes('youtube.com/watch') || url.includes('youtu.be/') || url.includes('youtube.com/shorts/')) {
      showYouTubeModal(url, 'YouTube Video');
    } else {
      downloadUrl(url);
    }
  }
});

downloadBtn.addEventListener('click', () => {
  const url = urlInput.value.trim();
  if (url.includes('youtube.com/watch') || url.includes('youtu.be/') || url.includes('youtube.com/shorts/')) {
    showYouTubeModal(url, 'YouTube Video');
  } else {
    downloadUrl(url);
  }
});

// Initialize
checkStatus();
getDetectedLinks();
renderHistory();
fetchActiveDownloads();

// Periodic updates
setInterval(checkStatus, 5000);
setInterval(fetchActiveDownloads, 2000);

// Format bytes
function formatBytes(bytes) {
  if (!bytes || bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
}

// Format speed
function formatSpeed(bytesPerSec) {
  if (!bytesPerSec || bytesPerSec === 0) return '';
  return formatBytes(bytesPerSec) + '/s';
}

// Fetch active downloads from server
async function fetchActiveDownloads() {
  try {
    const response = await fetch('http://localhost:19874/api/downloads', {
      signal: AbortSignal.timeout(2000)
    });
    const data = await response.json();
    renderActiveDownloads(data.downloads || []);
  } catch {
    renderActiveDownloads([]);
  }
}

// Render active downloads
function renderActiveDownloads(downloads) {
  activeDownloadCount.textContent = downloads.length;
  
  if (!downloads.length) {
    activeDownloads.innerHTML = `
      <div class="empty-state">
        <div class="icon">&#128229;</div>
        <div class="text">Aktif indirme yok</div>
      </div>`;
    return;
  }
  
  activeDownloads.innerHTML = downloads.map(dl => {
    const statusText = dl.status === 'downloading' ? 'Indiriliyor' :
                       dl.status === 'queued' ? 'Sirada' :
                       dl.status === 'paused' ? 'Duraklatildi' : dl.status;
    
    const fileName = dl.fileName || 'Dosya';
    const progress = dl.progress || 0;
    const downloaded = formatBytes(dl.downloadedBytes);
    const fileSize = dl.fileSize > 0 ? formatBytes(dl.fileSize) : '';
    const speed = formatSpeed(dl.speed);
    const started = dl.startedAt || '';
    
    return `
      <div class="download-item">
        <div class="download-item-header">
          <div class="download-item-name" title="${escapeHtml(fileName)}">${escapeHtml(fileName)}</div>
          <div class="download-item-status ${dl.status}">${statusText}</div>
        </div>
        <div class="download-progress-bar">
          <div class="download-progress-fill" style="width: ${progress}%"></div>
        </div>
        <div class="download-item-info">
          <span>${downloaded}${fileSize ? ' / ' + fileSize : ''} (${progress.toFixed(1)}%)</span>
          <span class="download-item-speed">${speed}</span>
        </div>
      </div>
    `;
  }).join('');
}
