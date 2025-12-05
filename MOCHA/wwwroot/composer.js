(function () {
    const handlers = new Map();
    let nextId = 1;

    function isEnterWithoutShift(event) {
        return event.key === "Enter" && event.shiftKey !== true;
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

            handlers.set(id, { element, handler });
            element.addEventListener("keydown", handler, true);
            return id;
        },
        detachEnterHandler: function (id) {
            const entry = handlers.get(id);
            if (!entry) {
                return;
            }

            entry.element.removeEventListener("keydown", entry.handler, true);
            handlers.delete(id);
        }
    };
})();
