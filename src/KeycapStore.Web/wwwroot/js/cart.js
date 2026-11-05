(function () {
    function celebrate(button) {
        const label = button.dataset.addedLabel;
        if (!label || button.dataset.original) {
            return;
        }

        button.dataset.original = button.textContent;
        button.textContent = label;
        setTimeout(() => {
            button.textContent = button.dataset.original;
            delete button.dataset.original;
        }, 1600);
    }

    async function add(form) {
        const store = window.keycapStore;
        const button = form.querySelector("button[type=submit]");
        button.disabled = true;
        button.setAttribute("aria-busy", "true");

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { Accept: "application/json" },
            });

            if (response.redirected) {
                window.location.href = response.url;
                return;
            }

            const json = (response.headers.get("content-type") ?? "").includes("application/json") ? await response.json() : null;
            if (response.ok && json) {
                celebrate(button);
                await store.flyToCart(button, form.dataset.cap);
                store.setCartCount(json.count);
                store.showToast(store.messages.added, "success");
            } else if (response.status === 409 && json?.message) {
                store.showToast(json.message, "error");
            } else {
                store.showToast(store.messages.failed, "error");
            }
        } catch {
            store.showToast(store.messages.failed, "error");
        } finally {
            button.disabled = false;
            button.removeAttribute("aria-busy");
        }
    }

    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute("data-cart-add") || !window.keycapStore || !window.fetch) {
            return;
        }

        event.preventDefault();
        add(form);
    });
})();
