// ES module exposing observe/disconnect for mini boards. Loaded via Blazor "import" from components.

const observers = new WeakMap();

function computeCount(container, tileW, tileH, gap){
    if (!container || !container.isConnected) return 0;
    const rect = container.getBoundingClientRect();
    const availableW = Math.max(0, rect.width);
    // Try to use vertical space from this grid's top to viewport bottom
    const viewportH = window.innerHeight || document.documentElement.clientHeight || 0;
    const availableH = Math.max(0, viewportH - rect.top - 8);
    const trackW = tileW + gap;
    const trackH = tileH + gap;
    const cols = Math.max(0, Math.floor((availableW + gap) / trackW));
    const rows = Math.max(0, Math.floor((availableH + gap) / trackH));
    const count = cols * rows;
    return count;
}

export function observe(container, dotnetRef, tileW, tileH, gap, max){
    // Fallbacks
    tileW = Number(tileW) || 176;
    tileH = Number(tileH) || 184;
    gap  = Number(gap)  || 12;
    max  = (max == null) ? null : Number(max);

    const notify = () => {
        let n = computeCount(container, tileW, tileH, gap);
        if (max != null) n = Math.min(n, max);
        // Always send at least 0; let .NET side ignore repeats
        try { dotnetRef.invokeMethodAsync('SetMiniBoardCount', n); } catch {}
    };

    // Initial and a couple of follow-ups in case layout settles after first paint
    notify();
    // Re-measure on next frame and shortly after to catch late layout/scrollbars
    try { requestAnimationFrame(() => notify()); } catch {}
    setTimeout(() => notify(), 60);

    const ro = new ResizeObserver(() => notify());
    ro.observe(container);
    observers.set(container, { ro, handler: notify });
    // Also listen to window resize (orientation changes)
    window.addEventListener('resize', notify);
}

export function disconnect(container){
    const entry = observers.get(container);
    if (entry){
        try { entry.ro.disconnect(); } catch {}
        window.removeEventListener('resize', entry.handler);
        observers.delete(container);
    }
}
