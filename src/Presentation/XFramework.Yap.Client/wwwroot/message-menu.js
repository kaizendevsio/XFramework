(() => {
    const observers = new WeakMap();
    window.yap.messageMenu = {
        open(dialog, stack, messageId) {
            const source = document.querySelector(`[data-message-id="${CSS.escape(messageId)}"]`);
            const preview = stack.querySelector('[data-menu-photo]');
            const image = source?.querySelector('.photo-open img');
            if (image?.complete && image.naturalWidth && preview) {
                const thumbnail = document.createElement('img');
                thumbnail.src = image.currentSrc || image.src;
                thumbnail.alt = 'Selected photo';
                preview.replaceChildren(thumbnail);
            }
            window.yap.haptics?.buzz('press');
            dialog.showModal();
            const place = () => {
                const viewportHeight = window.visualViewport?.height || window.innerHeight;
                const offset = window.visualViewport?.offsetTop || 0;
                dialog.style.setProperty('--menu-viewport-height', `${viewportHeight}px`);
                dialog.style.setProperty('--menu-viewport-top', `${offset}px`);
                // offsetHeight ignores the entry transform, so placement stays steady during animation.
                const height = stack.offsetHeight;
                const bubble = source?.querySelector('.bub,.photo-open') || source;
                const top = (bubble?.getBoundingClientRect().top ?? viewportHeight / 2 + offset) - offset;
                stack.style.setProperty('--menu-top', `${Math.max(16, Math.min(top, viewportHeight - height - 16))}px`);
            };
            const observer = new ResizeObserver(place);
            observer.observe(stack);
            window.visualViewport?.addEventListener('resize', place);
            window.visualViewport?.addEventListener('scroll', place);
            window.addEventListener('resize', place);
            observers.set(dialog, { observer, place }); place();
        },
        close(dialog) {
            const state = observers.get(dialog);
            if (state) {
                state.observer.disconnect();
                window.visualViewport?.removeEventListener('resize', state.place);
                window.visualViewport?.removeEventListener('scroll', state.place);
                window.removeEventListener('resize', state.place);
                observers.delete(dialog);
            }
            if (dialog?.open) dialog.close();
        }
    };
})();
