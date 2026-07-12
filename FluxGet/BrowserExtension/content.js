// FluxGet Content Script
// Extracts download links from web pages

(() => {
  if (window.__fluxgetInjected) return;
  window.__fluxgetInjected = true;

  function extractLinks() {
    const links = [];
    const seen = new Set();

    // YouTube sayfasi mi kontrol et
    if (isYouTubePage()) {
      const pageUrl = window.location.href;
      links.push({
        url: pageUrl,
        filename: getYouTubeTitle() || 'YouTube Video',
        type: 'video',
        text: getYouTubeTitle() || 'YouTube Video',
        isYouTube: true
      });
      return links;
    }

    document.querySelectorAll('a[href]').forEach(a => {
      const url = a.href;
      if (!url || seen.has(url)) return;
      if (!url.startsWith('http://') && !url.startsWith('https://')) return;
      if (url.startsWith('blob:') || url.startsWith('data:') || url.startsWith('javascript:')) return;
      if (url.includes('#') && url.split('#')[0] === window.location.href.split('#')[0]) return;

      const filename = extractFilename(url);
      const type = detectContentType(url);
      
      seen.add(url);
      links.push({ url, filename, type, text: a.textContent?.trim().substring(0, 100) || filename });
    });

    document.querySelectorAll('video source, video[src]').forEach(v => {
      const url = v.src || v.getAttribute('src');
      if (url && !seen.has(url) && !url.startsWith('blob:') && !url.startsWith('data:')) {
        seen.add(url);
        links.push({ url, filename: extractFilename(url), type: 'video', text: 'Video' });
      }
    });

    document.querySelectorAll('audio source, audio[src]').forEach(a => {
      const url = a.src || a.getAttribute('src');
      if (url && !seen.has(url) && !url.startsWith('blob:') && !url.startsWith('data:')) {
        seen.add(url);
        links.push({ url, filename: extractFilename(url), type: 'audio', text: 'Audio' });
      }
    });

    document.querySelectorAll('img[src]').forEach(img => {
      const url = img.src;
      if (url && !seen.has(url) && !url.startsWith('data:') && !url.startsWith('blob:')) {
        seen.add(url);
        links.push({ url, filename: extractFilename(url), type: 'image', text: img.alt || 'Image' });
      }
    });

    document.querySelectorAll('object[data], embed[src]').forEach(obj => {
      const url = obj.data || obj.src;
      if (url && !seen.has(url) && !url.startsWith('blob:')) {
        seen.add(url);
        links.push({ url, filename: extractFilename(url), type: detectContentType(url), text: 'Embedded content' });
      }
    });

    return links;
  }

  function isYouTubePage() {
    const host = window.location.hostname;
    return host.includes('youtube.com') || host.includes('youtu.be');
  }

  function getYouTubeTitle() {
    // YouTube video basligini al
    const titleEl = document.querySelector('h1.ytd-watch-metadata yt-formatted-string, #title h1 yt-formatted-string, h1.title');
    if (titleEl) return titleEl.textContent?.trim();
    
    // Alternatif: meta tag
    const metaTitle = document.querySelector('meta[name="title"]');
    if (metaTitle) return metaTitle.getAttribute('content');
    
    // Alternatif: title tag
    return document.title?.replace(' - YouTube', '').trim();
  }

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

  chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.action === 'getLinks') {
      const links = extractLinks();
      sendResponse({ links: links.slice(0, 100) });
    }
    return true;
  });
})();
