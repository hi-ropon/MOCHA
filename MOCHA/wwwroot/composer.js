(function () {
    const enterHandlers = new Map();
    const resizeHandlers = new Map();
    const pasteHandlers = new Map();
    let nextId = 1;
    const maxPasteSize = 1600;

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

    function resizeImageData(dataUrl, contentType, onReady) {
        const image = new Image();
        image.onload = () => {
            const maxSide = Math.max(image.width, image.height);
            const scale = maxSide > maxPasteSize ? maxPasteSize / maxSide : 1;
            const width = Math.max(1, Math.round(image.width * scale));
            const height = Math.max(1, Math.round(image.height * scale));

            const canvas = document.createElement("canvas");
            canvas.width = width;
            canvas.height = height;
            const ctx = canvas.getContext("2d");
            ctx.drawImage(image, 0, 0, width, height);

            const targetType = contentType === "image/png" ? "image/png" : "image/jpeg";
            const resized = canvas.toDataURL(targetType, 0.92);
            onReady(resized, targetType);
        };
        image.onerror = () => onReady(dataUrl, contentType);
        image.src = dataUrl;
    }

    function handleImageFile(file, dotNetRef) {
        const reader = new FileReader();
        reader.onload = () => {
            const dataUrl = reader.result;
            if (typeof dataUrl !== "string") {
                return;
            }

            resizeImageData(dataUrl, file.type || "image/png", (resized, actualType) => {
                dotNetRef.invokeMethodAsync("OnImagePastedAsync", file.name, actualType, resized);
            });
        };
        reader.readAsDataURL(file);
    }

    function handlePaste(event, dotNetRef) {
        if (!event.clipboardData || !dotNetRef) {
            return;
        }

        const files = Array.from(event.clipboardData.files || []);
        const items = Array.from(event.clipboardData.items || []);

        const images = files.filter(f => f.type && f.type.startsWith("image/"));
        if (images.length === 0) {
            items.forEach(item => {
                if (item.kind === "file" && item.type.startsWith("image/")) {
                    const file = item.getAsFile();
                    if (file) {
                        images.push(file);
                    }
                }
            });
        }

        if (images.length === 0) {
            return;
        }

        event.preventDefault();

        images.forEach(file => {
            handleImageFile(file, dotNetRef);
        });
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
        attachPasteHandler: function (element, dotNetRef) {
            if (!element || !dotNetRef) {
                return 0;
            }

            const id = nextId++;
            const handler = function (event) {
                handlePaste(event, dotNetRef);
            };

            pasteHandlers.set(id, { element, handler });
            element.addEventListener("paste", handler, true);
            return id;
        },
        detachPasteHandler: function (id) {
            const entry = pasteHandlers.get(id);
            if (!entry) {
                return;
            }

            entry.element.removeEventListener("paste", entry.handler, true);
            pasteHandlers.delete(id);
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
        },
        scrollToBottom: function (element) {
            if (!element) {
                return;
            }

            element.scrollTop = element.scrollHeight;
        }
    };
})();
