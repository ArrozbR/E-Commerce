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

    async function send(form, submitter) {
        const body = new FormData(form);
        if (submitter?.name) {
            body.set(submitter.name, submitter.value);
        }

        const response = await fetch(form.action, { method: "POST", body, headers: { Accept: "application/json" } });
        if (response.redirected) {
            window.location.href = response.url;
            return null;
        }

        const json = (response.headers.get("content-type") ?? "").includes("application/json") ? await response.json() : null;
        return { response, json };
    }

    function render(state) {
        const store = window.keycapStore;
        store.setCartCount(state.count);

        if (state.lines.length === 0) {
            window.location.reload();
            return;
        }

        document.querySelectorAll("[data-cart-units]").forEach((element) => {
            element.textContent = String(state.count);
        });
        document.querySelectorAll("[data-cart-total]").forEach((element) => {
            element.textContent = state.total;
        });
        document.querySelector("[data-cart-unavailable]")?.toggleAttribute("hidden", !state.hasUnavailableItems);

        state.lines.forEach((line) => {
            const item = document.querySelector(`[data-cart-line="${line.productId}"]`);
            if (!item) {
                return;
            }

            item.querySelector("[data-line-total]").textContent = line.lineTotal;
            const value = item.querySelector("[data-quantity]");
            value.textContent = String(line.quantity);
            value.dataset.max = String(line.max);
            item.querySelectorAll("[data-step]").forEach((stepButton) => {
                const next = line.quantity + Number(stepButton.dataset.step);
                stepButton.value = String(next);
                stepButton.disabled = next < 1 || next > line.max;
            });
        });
    }

    async function changeCart(form, submitter, removing) {
        const store = window.keycapStore;
        const item = form.closest("[data-cart-line]");
        const buttons = item ? item.querySelectorAll("button") : [];
        buttons.forEach((element) => {
            element.disabled = true;
        });

        try {
            const result = await send(form, submitter);
            if (!result) {
                return;
            }

            if (result.response.ok && result.json) {
                if (removing && item && store.animate) {
                    await new Promise((resolve) => window.gsap.to(item, { opacity: 0, height: 0, marginBottom: -16, paddingTop: 0, paddingBottom: 0, duration: 0.35, ease: "power2.in", onComplete: resolve }));
                }

                if (removing) {
                    item?.remove();
                }

                render(result.json);
                return;
            }

            store.showToast(result.response.status === 409 && result.json?.message ? result.json.message : store.messages.failed, "error");
        } catch {
            store.showToast(store.messages.failed, "error");
        } finally {
            if (item?.isConnected) {
                const value = item.querySelector("[data-quantity]");
                const quantity = Number(value.textContent);
                const max = Number(value.dataset.max);
                item.querySelectorAll("[data-step]").forEach((stepButton) => {
                    const next = quantity + Number(stepButton.dataset.step);
                    stepButton.disabled = next < 1 || next > max;
                });
                item.querySelectorAll("[data-cart-remove] button").forEach((element) => {
                    element.disabled = false;
                });
            }
        }
    }

    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !window.keycapStore || !window.fetch) {
            return;
        }

        if (form.hasAttribute("data-cart-add")) {
            event.preventDefault();
            add(form);
        } else if (form.hasAttribute("data-cart-quantity")) {
            event.preventDefault();
            changeCart(form, event.submitter, false);
        } else if (form.hasAttribute("data-cart-remove")) {
            event.preventDefault();
            changeCart(form, event.submitter, true);
        }
    });

    document.addEventListener("change", (event) => {
        const option = event.target;
        if (!(option instanceof HTMLInputElement) || !option.dataset.shipping) {
            return;
        }

        const shipping = document.querySelector("[data-summary-shipping]");
        const total = document.querySelector("[data-summary-total]");
        if (shipping && total) {
            shipping.textContent = option.dataset.shipping;
            shipping.classList.remove("text-muted");
            total.textContent = option.dataset.total;
        }
    });
})();
