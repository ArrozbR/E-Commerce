(function () {
    const root = document.documentElement;
    const systemDark = window.matchMedia("(prefers-color-scheme: dark)");
    const order = ["system", "light", "dark"];

    function stored() {
        try {
            return localStorage.getItem("theme") ?? "system";
        } catch {
            return "system";
        }
    }

    function remember(mode) {
        try {
            if (mode === "system") {
                localStorage.removeItem("theme");
            } else {
                localStorage.setItem("theme", mode);
            }
        } catch {
        }
    }

    function apply(mode) {
        root.classList.toggle("dark", mode === "dark" || (mode === "system" && systemDark.matches));

        const button = document.getElementById("theme-toggle");
        if (!button) {
            return;
        }

        const label = button.getAttribute("data-label-" + mode);
        button.dataset.mode = mode;
        button.setAttribute("aria-label", label);
        button.title = label;
    }

    document.addEventListener("DOMContentLoaded", function () {
        apply(stored());

        const button = document.getElementById("theme-toggle");
        if (button) {
            button.addEventListener("click", function () {
                const next = order[(order.indexOf(stored()) + 1) % order.length];
                remember(next);
                apply(next);
            });
        }
    });

    systemDark.addEventListener("change", function () {
        if (stored() === "system") {
            apply("system");
        }
    });
})();
