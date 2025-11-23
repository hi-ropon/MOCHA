(function () {
    const key = "mocha.preferences";
    const fallback = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches
        ? "dark"
        : "light";
    let theme = fallback;

    try {
        const stored = window.localStorage.getItem(key);
        if (stored) {
            const parsed = JSON.parse(stored);
            theme = parsed.Theme || parsed.theme || fallback;
        }
    } catch {
        theme = fallback;
    }

    document.documentElement.dataset.theme = theme === "dark" ? "dark" : "light";
})();

window.mochaPreferences = {
    getStoredPreferences: function (key) {
        try {
            return window.localStorage.getItem(key);
        } catch {
            return null;
        }
    },
    savePreferences: function (key, json) {
        try {
            window.localStorage.setItem(key, json);
        } catch {
            // ignore
        }
    },
    getPreferredColorScheme: function () {
        if (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches) {
            return "dark";
        }
        return "light";
    },
    applyTheme: function (theme) {
        var value = theme === "dark" ? "dark" : "light";
        document.documentElement.dataset.theme = value;
    }
};
