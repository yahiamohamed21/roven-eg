/**
 * ============================================================================
 * MAISON ROVEN - CINEMATIC SCROLL FRAME EXPERIENCE ENGINE
 * Ultra-High-Performance Canvas Scrubbing Engine (60fps / 120fps Fluidity)
 * 
 * Performance Architecture:
 *   - 70 optimized HD frames (1.88 MB total payload vs 8.8 MB)
 *   - Cloudflare cache-busted URLs (?v=v2_hd70) to prevent stale cache lock
 *   - Priority-driven dynamic preloader (scrub position jumps to front of queue)
 *   - Zero duplicate network requests via in-flight bitmask tracking
 *   - Dynamic aspect-ratio cover scaling via calcCover(img)
 *   - High-quality image smoothing on all renders
 *   - Debounced responsive canvas resizing with DPR pixel ratio scaling
 * ============================================================================
 */

(function () {
    'use strict';

    const TOTAL_FRAMES = 70;
    const FRAME_DIR = '/frames/';
    const FRAME_VERSION = 'v3_hq1600';
    const MAX_CONCURRENCY = 6;

    function getFrameUrl(index) {
        const padded = String(index).padStart(4, '0');
        return `${FRAME_DIR}frame_${padded}.webp?v=${FRAME_VERSION}`;
    }

    // Image Cache & State
    const images = new Array(TOTAL_FRAMES + 1);
    const loadedBitmask = new Uint8Array(TOTAL_FRAMES + 1);
    const inFlightBitmask = new Uint8Array(TOTAL_FRAMES + 1);
    const loadQueue = [];
    let activeLoads = 0;

    let currentFrameIndex = 1;
    let isTicking = false;

    // Metrics & State
    let canvasWidth = 0;
    let canvasHeight = 0;
    let drawX = 0, drawY = 0, drawWidth = 0, drawHeight = 0;
    let currentImg = null;

    // DOM Elements
    let section, track, canvas, ctx;
    let stageIntro;
    let scrollHint;

    function initElements() {
        section = document.getElementById('rovenCinematicSection');
        if (!section) return false;

        track = document.getElementById('rovenCinematicTrack');
        canvas = document.getElementById('rovenCinematicCanvas');
        if (!canvas) return false;

        ctx = canvas.getContext('2d', { alpha: false, desynchronized: true });

        stageIntro = document.getElementById('cinemaStageIntro');
        scrollHint = document.getElementById('rovenScrollHint');

        return true;
    }

    function calcCover(img) {
        const scale = Math.max(
            canvasWidth / img.naturalWidth,
            canvasHeight / img.naturalHeight
        );
        drawWidth = img.naturalWidth * scale;
        drawHeight = img.naturalHeight * scale;
        drawX = (canvasWidth - drawWidth) / 2;
        drawY = (canvasHeight - drawHeight) / 2;
    }

    function resizeCanvas() {
        if (!canvas || !ctx) return;
        const dpr = Math.min(window.devicePixelRatio || 1, 2);

        canvasWidth = window.innerWidth;   // وحدات CSS
        canvasHeight = window.innerHeight;

        canvas.width = Math.round(canvasWidth * dpr);    // الحجم الداخلي الحقيقي
        canvas.height = Math.round(canvasHeight * dpr);
        canvas.style.width = canvasWidth + 'px';         // الحجم المعروض
        canvas.style.height = canvasHeight + 'px';

        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

        if (currentImg) drawFrame(currentImg);
    }

    function drawFrame(img) {
        if (!ctx || !canvas || !img || !img.complete || img.naturalWidth === 0) return;

        currentImg = img;
        calcCover(img);

        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = 'high';

        ctx.fillStyle = '#080500';
        ctx.fillRect(0, 0, canvasWidth, canvasHeight);
        ctx.drawImage(img, drawX, drawY, drawWidth, drawHeight);
    }

    // Instant nearest-loaded frame search (never freezes in the middle)
    function getBestAvailableFrame(targetIndex) {
        if (loadedBitmask[targetIndex] && images[targetIndex]) {
            return images[targetIndex];
        }

        // Scan outward for closest ready frame
        for (let offset = 1; offset < TOTAL_FRAMES; offset++) {
            const prev = targetIndex - offset;
            if (prev >= 1 && loadedBitmask[prev] && images[prev]) {
                return images[prev];
            }
            const next = targetIndex + offset;
            if (next <= TOTAL_FRAMES && loadedBitmask[next] && images[next]) {
                return images[next];
            }
        }

        return images[1] || null;
    }

    // Dynamic Priority Queue Loader
    function enqueueFrame(index, highPriority = false) {
        if (index < 1 || index > TOTAL_FRAMES || loadedBitmask[index] || inFlightBitmask[index]) {
            return;
        }

        inFlightBitmask[index] = 1;
        if (highPriority) {
            loadQueue.unshift(index);
        } else {
            loadQueue.push(index);
        }
        pumpQueue();
    }

    function pumpQueue() {
        while (activeLoads < MAX_CONCURRENCY && loadQueue.length > 0) {
            const nextIdx = loadQueue.shift();
            if (!nextIdx || loadedBitmask[nextIdx]) {
                continue;
            }

            activeLoads++;
            const img = new Image();
            img.src = getFrameUrl(nextIdx);

            img.onload = function () {
                images[nextIdx] = img;
                loadedBitmask[nextIdx] = 1;
                inFlightBitmask[nextIdx] = 0;
                activeLoads--;

                // If user is scrubbing at or near this frame, render immediately
                if (currentFrameIndex === nextIdx) {
                    drawFrame(img);
                } else if (!loadedBitmask[currentFrameIndex]) {
                    const best = getBestAvailableFrame(currentFrameIndex);
                    if (best === img) {
                        drawFrame(img);
                    }
                }
                pumpQueue();
            };

            img.onerror = function () {
                inFlightBitmask[nextIdx] = 0;
                activeLoads--;
                pumpQueue();
            };
        }
    }

    // Pull immediate surrounding frames to the front of the queue
    function prioritizeWindow(targetIndex, radius = 6) {
        enqueueFrame(targetIndex, true);
        for (let offset = 1; offset <= radius; offset++) {
            const fwd = targetIndex + offset;
            if (fwd <= TOTAL_FRAMES) enqueueFrame(fwd, true);
            const bwd = targetIndex - offset;
            if (bwd >= 1) enqueueFrame(bwd, true);
        }
    }

    // Render frame by index
    function renderFrame(index) {
        currentFrameIndex = Math.min(TOTAL_FRAMES, Math.max(1, index));

        // Prioritize frames directly around current scroll position
        prioritizeWindow(currentFrameIndex, 6);

        const img = getBestAvailableFrame(currentFrameIndex);
        if (img) {
            drawFrame(img);
        }

        // Update Intro Text Opacity
        const pct = (currentFrameIndex - 1) / (TOTAL_FRAMES - 1);
        updateTextStages(pct);
    }

    // Text appears at the beginning only, and vanishes completely once user starts scrolling
    function updateTextStages(progress) {
        if (!stageIntro) return;

        if (progress <= 0.04) {
            stageIntro.style.opacity = '1';
            stageIntro.style.transform = 'translateY(0) scale(1)';
            stageIntro.style.pointerEvents = 'auto';
            stageIntro.style.visibility = 'visible';
        } else if (progress < 0.12) {
            const ratio = 1 - ((progress - 0.04) / 0.08);
            stageIntro.style.opacity = Math.max(0, ratio).toFixed(3);
            stageIntro.style.transform = `translateY(${(-18 * (1 - ratio)).toFixed(1)}px) scale(${(0.98 + 0.02 * ratio).toFixed(3)})`;
            stageIntro.style.pointerEvents = 'none';
            stageIntro.style.visibility = 'visible';
        } else {
            stageIntro.style.opacity = '0';
            stageIntro.style.transform = 'translateY(-18px) scale(0.98)';
            stageIntro.style.pointerEvents = 'none';
            stageIntro.style.visibility = 'hidden';
        }
    }

    // High-performance scroll handler (0 layout thrashing, 60-120fps)
    function onScroll() {
        if (!track) return;

        const rect = track.getBoundingClientRect();
        const maxScroll = track.offsetHeight - window.innerHeight;

        if (rect.top <= 0 && rect.bottom >= window.innerHeight) {
            const currentScroll = -rect.top;
            const progress = Math.min(1, Math.max(0, currentScroll / maxScroll));
            const targetFrame = Math.min(TOTAL_FRAMES, Math.max(1, Math.round(progress * (TOTAL_FRAMES - 1)) + 1));

            if (targetFrame !== currentFrameIndex) {
                renderFrame(targetFrame);
            }
        } else if (rect.top > 0) {
            if (currentFrameIndex !== 1) renderFrame(1);
        } else if (rect.bottom < window.innerHeight) {
            if (currentFrameIndex !== TOTAL_FRAMES) renderFrame(TOTAL_FRAMES);
        }
    }

    function requestScrollTick() {
        if (!isTicking) {
            requestAnimationFrame(() => {
                onScroll();
                isTicking = false;
            });
            isTicking = true;
        }
    }

    // Interactive discovery click handler
    function bindInteractions() {
        if (scrollHint) {
            scrollHint.addEventListener('click', function () {
                if (!track) return;
                const rect = track.getBoundingClientRect();
                const scrollTarget = window.pageYOffset + rect.top + (track.offsetHeight * 0.35);
                window.scrollTo({ top: scrollTarget, behavior: 'smooth' });
            });
        }
    }

    // Initialize on DOM Ready
    function init() {
        if (!initElements()) return;

        // استدعاء أول مرة
        resizeCanvas();

        // عند تغيير حجم الشاشة (مع debounce)
        let resizeTimer;
        window.addEventListener('resize', () => {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(resizeCanvas, 120);
        }, { passive: true });

        // 1. Immediately prioritize Frame 1
        enqueueFrame(1, true);

        // 2. Prioritize keyframes evenly distributed across the entire 70-frame range
        // [10, 20, 30, 40, 50, 60, 70]
        for (let i = 10; i <= TOTAL_FRAMES; i += 10) {
            enqueueFrame(i, true);
        }

        // 3. Enqueue all remaining frames to preload smoothly in the background
        for (let i = 2; i <= TOTAL_FRAMES; i++) {
            enqueueFrame(i, false);
        }

        bindInteractions();

        window.addEventListener('scroll', requestScrollTick, { passive: true });

        // Draw initial frame immediately
        renderFrame(1);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
