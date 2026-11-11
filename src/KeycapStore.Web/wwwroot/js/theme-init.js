(function () {
    try {
        const theme = localStorage.getItem("theme");
        document.documentElement.classList.toggle("dark", theme === "dark" || (theme === null && window.matchMedia("(prefers-color-scheme: dark)").matches));
    } catch {
        document.documentElement.classList.toggle("dark", window.matchMedia("(prefers-color-scheme: dark)").matches);
    }

    if (!window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
        document.documentElement.classList.add("motion");
    }
})();
