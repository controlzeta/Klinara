(() => {
    const storageKey = "klinara-theme";

    function setTheme(theme) {
        document.documentElement.dataset.theme = theme;
        document.documentElement.dataset.bsTheme = theme;
        try {
            localStorage.setItem(storageKey, theme);
        } catch {
            // Theme remains active for the current page if storage is unavailable.
        }
    }

    window.klinaraUi = {
        initialize() {
            let theme = "light";
            try {
                theme = localStorage.getItem(storageKey) || "light";
            } catch {
                // Keep the default light theme if storage is unavailable.
            }
            document.documentElement.dataset.theme = theme;
            document.documentElement.dataset.bsTheme = theme;
        },
        toggleTheme() {
            const nextTheme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
            setTheme(nextTheme);
            return nextTheme;
        },
        toggleMenu() {
            document.querySelector(".page.klinara-shell")?.classList.toggle("menu-open");
        },
        closeMenu() {
            document.querySelector(".page.klinara-shell")?.classList.remove("menu-open");
        }
    };

    window.klinaraUi.initialize();
})();
