(function () {
    const enterHandlers = new Map();
    const resizeHandlers = new Map();
    let nextId = 1;

    function isEnterWithoutShift(event) {
        return event.key === "Enter" && event.shiftKey !== true;
    }

    function parsePx(value) {
        const parsed = parseFloat(value);
        return Number.isNaN(parsed) ? 0 : parsed;
    }

    function measureHeights(element, maxLines) {
        const style = window.getComputedStyle(element);
        const lineHeight = parsePx(style.lineHeight) || 20;
        const padding = parsePx(style.paddingTop) + parsePx(style.paddingBottom);
        const border = parsePx(style.borderTopWidth) + parsePx(style.borderBottomWidth);
        const minHeight = lineHeight + padding + border;
        const maxHeight = lineHeight * maxLines + padding + border;

        return { minHeight, maxHeight };
    }

    function applyAutoResize(element, maxLines) {
        if (!element) {
            return;
        }

        const { minHeight, maxHeight } = measureHeights(element, maxLines);
        element.style.minHeight = `${minHeight}px`;
        element.style.maxHeight = `${maxHeight}px`;
        element.style.height = "auto";

        const nextHeight = Math.min(Math.max(element.scrollHeight, minHeight), maxHeight);
        element.style.height = `${nextHeight}px`;
    }

    window.mochaComposer = {
        attachEnterHandler: function (element) {
            if (!element) {
                return 0;
            }

            const id = nextId++;
            const handler = function (event) {
                if (isEnterWithoutShift(event)) {
                    event.preventDefault();
                }
            };

            enterHandlers.set(id, { element, handler });
            element.addEventListener("keydown", handler, true);
            return id;
        },
        detachEnterHandler: function (id) {
            const entry = enterHandlers.get(id);
            if (!entry) {
                return;
            }

            entry.element.removeEventListener("keydown", entry.handler, true);
            enterHandlers.delete(id);
        },
        attachAutoResize: function (element, maxLines) {
            if (!element) {
                return 0;
            }

            const id = nextId++;
            const handler = function () {
                applyAutoResize(element, maxLines);
            };

            resizeHandlers.set(id, { element, handler });
            element.addEventListener("input", handler, true);
            requestAnimationFrame(handler);
            return id;
        },
        detachAutoResize: function (id) {
            const entry = resizeHandlers.get(id);
            if (!entry) {
                return;
            }

            entry.element.removeEventListener("input", entry.handler, true);
            resizeHandlers.delete(id);
        },
        resize: function (element, maxLines) {
            applyAutoResize(element, maxLines);
        }
    };
})();
