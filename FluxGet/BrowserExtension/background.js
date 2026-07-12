const API_URL = 'http://localhost:19874/api';
const HEALTH_CHECK_INTERVAL = 5000;

let isServerRunning = false;
let apiToken = null;

// Get API token from server
async function fetchApiToken() {
  try {
    const response = await fetch(`${API_URL}/token`, {
      method: 'GET',
      signal: AbortSignal.timeout(2000)
    });
    if (response.ok) {
      const data = await response.json();
      apiToken = data.token;
      return true;
    }
  } catch {}
  return false;
}

// Check if FluxGet is running
async function checkServerHealth() {
  try {
    const response = await fetch(`${API_URL}/health`, {
      method: 'GET',
      signal: AbortSignal.timeout(2000)
    });
    isServerRunning = response.ok;
    if (isServerRunning && !apiToken) {
      await fetchApiToken();
    }
  } catch {
    isServerRunning = false;
  }
}

// Send download request to FluxGet
async function sendDownloadRequest(url, filename = null, referrer = null, resolution = null, format = null) {
  if (!isServerRunning) {
    await checkServerHealth();
    if (!isServerRunning) {
      console.warn('FluxGet server is not running');
      return { success: false, error: 'FluxGet server is not running' };
    }
  }
  
  if (!apiToken) {
    await fetchApiToken();
    if (!apiToken) {
      console.warn('Could not get API token');
      return { success: false, error: 'Could not get API token' };
    }
  }

  try {
    const body = {
      url: url,
      filename: filename,
      referrer: referrer
    };
    
    if (resolution) {
      body.resolution = resolution;
    }
    
    if (format) {
      body.format = format;
    }

    const response = await fetch(`${API_URL}/download`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${apiToken}`
      },
      body: JSON.stringify(body)
    });

    const result = await response.json();
    
    // Token refresh needed
    if (response.status === 401) {
      apiToken = null;
      await fetchApiToken();
      return { success: false, error: 'Token refreshed, please try again' };
    }
    
    return result;
  } catch (error) {
    console.error('Download request failed:', error);
    return { success: false, error: error.message };
  }
}

// Extract filename from URL
function extractFilename(url) {
  try {
    const urlObj = new URL(url);
    const pathname = urlObj.pathname;
    const filename = decodeURIComponent(pathname.split('/').pop());
    return filename || 'download';
  } catch {
    return 'download';
  }
}

// Detect content type from URL
function detectContentType(url) {
  const ext = url.split('.').pop()?.split('?')[0]?.toLowerCase();
  
  const videoExts = ['mp4', 'avi', 'mkv', 'mov', 'wmv', 'flv', 'webm', 'm3u8', 'ts'];
  const audioExts = ['mp3', 'wav', 'flac', 'aac', 'ogg', 'wma', 'm4a'];
  const imageExts = ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'svg', 'webp', 'ico'];
  const docExts = ['pdf', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx', 'txt', 'csv'];
  const archiveExts = ['zip', 'rar', '7z', 'tar', 'gz', 'bz2'];
  
  if (videoExts.includes(ext)) return 'video';
  if (audioExts.includes(ext)) return 'audio';
  if (imageExts.includes(ext)) return 'image';
  if (docExts.includes(ext)) return 'document';
  if (archiveExts.includes(ext)) return 'archive';
  
  return 'general';
}

// Create context menu items
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: 'download-with-fluxget',
    title: 'Download with FluxGet',
    contexts: ['link', 'image', 'video', 'audio']
  });

  chrome.contextMenus.create({
    id: 'download-video-with-fluxget',
    title: 'Download video with FluxGet',
    contexts: ['video']
  });

  chrome.contextMenus.create({
    id: 'download-image-with-fluxget',
    title: 'Download image with FluxGet',
    contexts: ['image']
  });

  chrome.contextMenus.create({
    id: 'download-page-with-fluxget',
    title: 'Download all links on page',
    contexts: ['page']
  });

  chrome.contextMenus.create({
    id: 'download-selected-with-fluxget',
    title: 'Download selection with FluxGet',
    contexts: ['selection']
  });

  // Initial health check
  checkServerHealth();
});

// Handle context menu clicks
chrome.contextMenus.onClicked.addListener((info, tab) => {
  let url = info.linkUrl || info.srcUrl;
  
  if (info.menuItemId === 'download-with-fluxget') {
    if (url) {
      const filename = extractFilename(url);
      sendDownloadRequest(url, filename, tab?.url);
    }
  } else if (info.menuItemId === 'download-video-with-fluxget') {
    if (url) {
      sendDownloadRequest(url, null, tab?.url);
    }
  } else if (info.menuItemId === 'download-image-with-fluxget') {
    if (url) {
      const filename = extractFilename(url);
      sendDownloadRequest(url, filename, tab?.url);
    }
  } else if (info.menuItemId === 'download-page-with-fluxget') {
    downloadAllLinks(tab);
  } else if (info.menuItemId === 'download-selected-with-fluxget') {
    if (info.selectionText) {
      // Try to detect URL in selection
      const urlMatch = info.selectionText.match(/https?:\/\/[^\s]+/);
      if (urlMatch) {
        sendDownloadRequest(urlMatch[0], null, tab?.url);
      }
    }
  }
});

// Download all links on the page
async function downloadAllLinks(tab) {
  try {
    const response = await chrome.tabs.sendMessage(tab.id, { action: 'getLinks' });
    if (response && response.links) {
      for (const link of response.links) {
        await sendDownloadRequest(link.url, link.filename, tab.url);
        // Small delay between requests
        await new Promise(resolve => setTimeout(resolve, 100));
      }
    }
  } catch (error) {
    console.error('Could not get links from page:', error);
  }
}

// Handle messages from popup or content scripts
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.action === 'download') {
    sendDownloadRequest(message.url, message.filename, message.referrer, message.resolution, message.format)
      .then(result => sendResponse(result));
    return true;
  }
  
  if (message.action === 'checkHealth') {
    checkServerHealth().then(() => {
      sendResponse({ running: isServerRunning });
    });
    return true;
  }
  
  if (message.action === 'getStatus') {
    sendResponse({ running: isServerRunning });
    return false;
  }
});

// Periodic health check
setInterval(checkServerHealth, HEALTH_CHECK_INTERVAL);

// Initial health check
checkServerHealth();
