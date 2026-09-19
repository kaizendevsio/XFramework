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
                const height = stack.getBoundingClientRect().height;
                const viewportHeight = window.visualViewport?.height || window.innerHeight;
                const top = source?.getBoundingClientRect().top ?? viewportHeight / 2;
                stack.style.setProperty('--menu-top', `${Math.max(16, Math.min(top - 70, viewportHeight - height - 24))}px`);
            };
            const observer = new ResizeObserver(place);
            observer.observe(stack); observers.set(dialog, observer); place();
        },
        close(dialog) { observers.get(dialog)?.disconnect(); observers.delete(dialog); if (dialog?.open) dialog.close(); }
    };
})();
