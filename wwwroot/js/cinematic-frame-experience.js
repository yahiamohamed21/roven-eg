/**
 * ============================================================================
 * MAISON ROVEN - CINEMATIC SCROLL FRAME EXPERIENCE ENGINE
 * Ultra-Lightweight On-Demand Canvas Frame Scrubbing Engine
 * Features:
 *   - Intro text appears only at the start and vanishes cleanly upon scroll
 *   - 0ms initial load (Frame 1 instant)
 *   - Sliding lookahead window (Never storms the server with 200 requests)
 *   - Concurrency throttle (Max 3 concurrent image requests)
 *   - Device-adaptive aspect scaling (Zero mobile zoom distortion)
 *   - Luxury interactive discovery hint controls
 * ============================================================================
 */

(function () {
    'use strict';

    const TOTAL_FRAMES = 200;
    const FRAME_DIR = '/frames/';
    const MAX_CONCURRENT_LOADS = 3;
    const LOOKAHEAD_WINDOW = 12;
    const LOOKBEHIND_WINDOW = 4;

    function getFrameUrl(index) {
        const padded = String(index).padStart(4, '0');
        return `${FRAME_DIR}frame_${padded}.webp`;
    }

    // Cache image objects
    const images = new Array(TOTAL_FRAMES + 1);
    const loadingSet = new Set();
    const loadQueue = [];
    let activeLoads = 0;

    let currentFrameIndex = 1;
    let isTicking = false;

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

        ctx = canvas.getContext('2d', { alpha: false });

        stageIntro = document.getElementById('cinemaStageIntro');
        scrollHint = document.getElementById('rovenScrollHint');

        return true;
    }

    // Enqueue an individual frame for loading with concurrency throttling
    function requestFrameLoad(index, isHighPriority = false) {
        if (index < 1 || index > TOTAL_FRAMES) return;
        if (images[index] || loadingSet.has(index)) return;

        loadingSet.add(index);

        if (isHighPriority) {
            loadQueue.unshift(index);
        } else {
            loadQueue.push(index);
        }

        pumpLoadQueue();
    }

    function pumpLoadQueue() {
        while (activeLoads < MAX_CONCURRENT_LOADS && loadQueue.length > 0) {
            const nextIndex = loadQueue.shift();
            if (!nextIndex || images[nextIndex]) continue;

            activeLoads++;
            const img = new Image();
            img.src = getFrameUrl(nextIndex);

            img.onload = () => {
                images[nextIndex] = img;
                loadingSet.delete(nextIndex);
                activeLoads--;

                // If user is currently waiting on this exact frame, redraw immediately
                if (currentFrameIndex === nextIndex) {
                    drawFrame(img);
                }
                pumpLoadQueue();
            };

            img.onerror = () => {
                loadingSet.delete(nextIndex);
                activeLoads--;
                pumpLoadQueue();
            };
        }
    }

    // Adaptive lookahead window centered on current scroll position
    function ensureFramesAround(centerIndex) {
        // High priority: exact frame
        requestFrameLoad(centerIndex, true);

        // Forward window (direction of discovery)
        for (let i = 1; i <= LOOKAHEAD_WINDOW; i++) {
            const idx = centerIndex + i;
            if (idx <= TOTAL_FRAMES) requestFrameLoad(idx, false);
        }

        // Backward window
        for (let i = 1; i <= LOOKBEHIND_WINDOW; i++) {
            const idx = centerIndex - i;
            if (idx >= 1) requestFrameLoad(idx, false);
        }
    }

    // Draw frame on canvas with full-bleed cover fit across all screens (0% black bars)
    function drawFrame(img) {
        if (!ctx || !canvas || !img || !img.complete || img.naturalWidth === 0) return;

        const dpr = Math.min(window.devicePixelRatio || 1, 2); // Cap at 2 to conserve mobile GPU
        const rect = canvas.getBoundingClientRect();

        const canvasWidth = rect.width;
        const canvasHeight = rect.height;

        if (canvasWidth === 0 || canvasHeight === 0) return;

        // Ensure canvas dimensions match display
        if (canvas.width !== Math.floor(canvasWidth * dpr) || canvas.height !== Math.floor(canvasHeight * dpr)) {
            canvas.width = Math.floor(canvasWidth * dpr);
            canvas.height = Math.floor(canvasHeight * dpr);
            ctx.scale(dpr, dpr);
        }

        const imgWidth = img.naturalWidth;
        const imgHeight = img.naturalHeight;

        // Full-bleed cover scale: fills 100% of the screen with ZERO black letterbox bars
        const scale = Math.max(canvasWidth / imgWidth, canvasHeight / imgHeight);
        const drawWidth = imgWidth * scale;
        const drawHeight = imgHeight * scale;
        const drawX = (canvasWidth - drawWidth) / 2;
        const drawY = (canvasHeight - drawHeight) / 2;

        ctx.fillStyle = '#080500';
        ctx.fillRect(0, 0, canvasWidth, canvasHeight);
        ctx.drawImage(img, drawX, drawY, drawWidth, drawHeight);
    }

    // Render a specific frame by index with lookahead buffer trigger
    function renderFrame(index) {
        currentFrameIndex = Math.min(TOTAL_FRAMES, Math.max(1, index));

        // Trigger dynamic sliding lookahead
        ensureFramesAround(currentFrameIndex);

        const img = images[currentFrameIndex];
        if (img && img.complete && img.naturalWidth > 0) {
            drawFrame(img);
        } else {
            // Fallback to closest loaded frame while the target frame streams in
            let found = false;
            for (let offset = 1; offset < 25; offset++) {
                const prev = images[currentFrameIndex - offset];
                if (prev && prev.complete && prev.naturalWidth > 0) {
                    drawFrame(prev);
                    found = true;
                    break;
                }
                const next = images[currentFrameIndex + offset];
                if (next && next.complete && next.naturalWidth > 0) {
                    drawFrame(next);
                    found = true;
                    break;
                }
            }
            if (!found && images[1] && images[1].complete) {
                drawFrame(images[1]);
            }
        }

        // Update Text Stage Opacities
        const pct = ((currentFrameIndex - 1) / (TOTAL_FRAMES - 1));
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

    // Progressive Warmup: Load Frame 1 immediately, then warm up frames 2-12 gently
    function initProgressiveWarmup() {
        const firstImg = new Image();
        firstImg.src = getFrameUrl(1);
        images[1] = firstImg;

        firstImg.onload = function () {
            drawFrame(firstImg);
        };

        // Warm up initial 12 frames sequentially with low impact
        let warmupIndex = 2;
        function loadNextWarmup() {
            if (warmupIndex > 12) return;
            requestFrameLoad(warmupIndex, false);
            warmupIndex++;
            setTimeout(loadNextWarmup, 120);
        }

        setTimeout(loadNextWarmup, 300);
    }

    // Scroll Handler driven by requestAnimationFrame
    function onScroll() {
        if (!track) return;

        const rect = track.getBoundingClientRect();
        const maxScroll = track.offsetHeight - window.innerHeight;

        // If track is in view
        if (rect.top <= 0 && rect.bottom >= window.innerHeight) {
            const currentScroll = -rect.top;
            const progress = Math.min(1, Math.max(0, currentScroll / maxScroll));
            const targetFrame = Math.min(TOTAL_FRAMES, Math.max(1, Math.floor(progress * (TOTAL_FRAMES - 1)) + 1));

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

    // Resize Handler
    function onResize() {
        if (!canvas) return;
        renderFrame(currentFrameIndex);
    }

    // Initialize interactive discovery click handler
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

        initProgressiveWarmup();
        bindInteractions();

        window.addEventListener('scroll', requestScrollTick, { passive: true });
        window.addEventListener('resize', onResize, { passive: true });

        // Draw initial frame
        renderFrame(1);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();