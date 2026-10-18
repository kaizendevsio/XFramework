// The shell and worker are identical for every tenant. Branding is fetched through a
// network-only API and its last public value is stored in this origin for offline startup.
(() => {
    const defaults = { name: 'Yap', shortName: 'Yap', tagline: 'A little closer.', logoUrl: '', accentColor: '#d5f879',
        icon192Url: '/yap-app-v2-192.png', icon512Url: '/yap-app-v2-512.png', appleIconUrl: '/yap-app-v2-apple.png', manifestUrl: '/manifest.webmanifest' };
    const image = value => typeof value === 'string' && /^\/(?:[a-zA-Z0-9_-]+\/)*[a-zA-Z0-9_.-]+\.(?:png|svg|webp|jpg|jpeg)$/i.test(value) && !value.includes('..');
    const valid = value => value && ['name', 'shortName', 'tagline'].every(key => typeof value[key] === 'string' && value[key].length > 0 && value[key].length <= 160 && !/[\x00-\x1f\x7f]/.test(value[key]))
        && /^#[0-9a-f]{6}$/i.test(value.accentColor) && (!value.logoUrl || image(value.logoUrl))
        && ['icon192Url', 'icon512Url', 'appleIconUrl'].every(key => image(value[key]))
        && ['/manifest.webmanifest', '/api/branding/manifest'].includes(value.manifestUrl);
    const select = value => Object.fromEntries(Object.keys(defaults).map(key => [key, value[key]]));
    let current = defaults;
    try { const cached = JSON.parse(localStorage.getItem('yap-branding')); if (valid(cached)) current = select(cached); } catch {}
    const apply = brand => {
        document.title = `${brand.name} · ${brand.tagline}`;
        document.querySelector('meta[name="apple-mobile-web-app-title"]')?.setAttribute('content', brand.shortName);
        document.querySelector('meta[name="description"]')?.setAttribute('content', `${brand.name}. ${brand.tagline}`);
        document.querySelector('link[rel="manifest"]')?.setAttribute('href', brand.manifestUrl);
        document.querySelector('link[rel="apple-touch-icon"]')?.setAttribute('href', brand.appleIconUrl);
        if (brand.logoUrl) { const icon = document.querySelector('link[rel="icon"]'); icon?.setAttribute('href', brand.logoUrl); icon?.removeAttribute('type'); }
        // Keep the user's own accent choice; the tenant color is the default on this origin.
        yap.accentPreference = () => { try { const own = localStorage.getItem('yap-accent'); if (/^#[0-9a-f]{6}$/i.test(own)) return own; } catch {} return brand.accentColor; };
        yap.applyAccent(yap.accentPreference());
        if (brand.name !== 'Yap') document.querySelector('[data-startup-brand-name]')?.replaceChildren(brand.name);
        const text = document.querySelector('[data-startup-brand-text]'); if (text) text.textContent = `Starting ${brand.name}…`;
        if (brand.logoUrl) {
            const mark = document.querySelector('[data-startup-brand-mark]');
            if (mark) { const logo = document.createElement('img'); logo.src = brand.logoUrl; logo.alt = ''; logo.width = 43; logo.height = 43; mark.replaceChildren(logo); }
        }
    };
    apply(current);
    const ready = (async () => {
        const controller = new AbortController(), timer = setTimeout(() => controller.abort(), 3000);
        try {
            const response = await fetch('/api/branding', { cache: 'no-store', signal: controller.signal });
            if (response.ok) {
                const value = await response.json();
                if (valid(value)) { current = select(value); try { localStorage.setItem('yap-branding', JSON.stringify(current)); } catch {} apply(current); }
            }
        } catch { /* Offline startup retains the last public branding for this origin. */ }
        finally { clearTimeout(timer); }
        return current;
    })();
    yap.branding = { get: () => current, refresh: () => ready };
})();
