// Coarse platform only. Never upload the user agent, hardware IDs, IP addresses or media.
export function device() {
    if (/Android/i.test(navigator.userAgent)) return 1;
    if (/iPhone|iPad|iPod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)) return 2;
    return 3;
}
