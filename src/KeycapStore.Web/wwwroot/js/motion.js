(function () {
    const root = document.documentElement;
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const gsap = window.gsap;
    const animate = Boolean(gsap) && !reduced;

    function revealItems() {
        if (!root.classList.contains("motion")) {
            return;
        }

        const items = document.querySelectorAll("[data-reveal]");
        if (!animate || performance.now() > 1000) {
            root.classList.remove("motion");
            return;
        }

        root.classList.add("motion-ready");
        gsap.set(items, { y: 28 });

        const show = (batch) => gsap.to(batch, { opacity: 1, y: 0, duration: 0.6, ease: "power3.out", stagger: 0.08 });
        if (window.ScrollTrigger) {
            gsap.registerPlugin(window.ScrollTrigger);
            window.ScrollTrigger.batch(items, { start: "top 95%", once: true, onEnter: show });
        } else {
            show(items);
        }
    }

    function setupSpotlight() {
        if (reduced || !window.matchMedia("(pointer: fine)").matches) {
            return;
        }

        document.querySelectorAll(".card-interactive").forEach((card) => {
            card.addEventListener("pointermove", (event) => {
                const box = card.getBoundingClientRect();
                card.style.setProperty("--spot-x", `${event.clientX - box.left}px`);
                card.style.setProperty("--spot-y", `${event.clientY - box.top}px`);
            });
        });
    }

    function pressKey(key) {
        key.classList.add("is-pressed");
        key.dataset.pressedAt = String(performance.now());
    }

    function releaseKey(key) {
        const held = performance.now() - Number(key.dataset.pressedAt || 0);
        setTimeout(() => key.classList.remove("is-pressed"), Math.max(0, 90 - held));
    }

    function setupKeycaps() {
        document.addEventListener("pointerdown", (event) => {
            const key = event.target instanceof Element ? event.target.closest(".keycap") : null;
            if (!key) {
                return;
            }

            pressKey(key);
            const release = () => {
                releaseKey(key);
                window.removeEventListener("pointerup", release);
                window.removeEventListener("pointercancel", release);
            };
            window.addEventListener("pointerup", release);
            window.addEventListener("pointercancel", release);
        });
    }

    const keyAliases = {
        " ": "Space",
        Escape: "Esc",
        CapsLock: "Caps",
        Control: "Ctrl",
        Meta: "Win",
        OS: "Win",
        ContextMenu: "Menu",
        PrintScreen: "PrtSc",
        ScrollLock: "ScrLk",
    };

    function keyName(event) {
        if (keyAliases[event.key]) {
            return keyAliases[event.key];
        }

        return event.key.length === 1 ? event.key.toUpperCase() : event.key;
    }

    function setupTyping() {
        const stage = document.querySelector("[data-typeable]");
        if (!stage) {
            return;
        }

        const keysFor = (event) => stage.querySelectorAll(`.keycap[data-key="${CSS.escape(keyName(event))}"]`);
        const typing = (event) => !(event.target instanceof Element && event.target.closest("input, textarea, select, [contenteditable]"));

        document.addEventListener("keydown", (event) => {
            if (!event.repeat && typing(event)) {
                keysFor(event).forEach(pressKey);
            }
        });
        document.addEventListener("keyup", (event) => {
            keysFor(event).forEach(releaseKey);
        });

        if (animate) {
            const tops = stage.querySelectorAll(".keycap-top");
            gsap.to(tops, {
                y: (index, top) => parseFloat(getComputedStyle(top.parentElement).getPropertyValue("--press")) || 2,
                duration: 0.09,
                yoyo: true,
                repeat: 1,
                ease: "power1.out",
                delay: 0.5,
                stagger: (index, top) => parseFloat(getComputedStyle(top.parentElement).getPropertyValue("--d")) * 0.6,
            });
        }
    }

    const region = document.getElementById("toast-region");
    let activeToast = null;

    function closeToast(toast) {
        if (!toast.isConnected || toast.dataset.closing) {
            return;
        }

        toast.dataset.closing = "true";
        clearTimeout(Number(toast.dataset.timer));
        if (activeToast === toast) {
            activeToast = null;
        }

        if (animate) {
            gsap.to(toast, { opacity: 0, y: 16, duration: 0.2, ease: "power2.in", onComplete: () => toast.remove() });
        } else {
            toast.remove();
        }
    }

    function manageToast(toast, duration) {
        toast.style.animation = "none";
        activeToast = toast;

        const schedule = () => {
            clearTimeout(Number(toast.dataset.timer));
            toast.dataset.timer = String(setTimeout(() => closeToast(toast), duration));
        };
        const pause = () => clearTimeout(Number(toast.dataset.timer));

        toast.querySelector("[data-toast-close]")?.addEventListener("click", () => closeToast(toast));
        toast.addEventListener("mouseenter", pause);
        toast.addEventListener("mouseleave", schedule);
        toast.addEventListener("focusin", pause);
        toast.addEventListener("focusout", schedule);

        if (animate) {
            gsap.from(toast, { opacity: 0, y: 24, scale: 0.96, duration: 0.45, ease: "back.out(2)" });
        }

        schedule();
    }

    function showToast(message, kind) {
        const template = document.getElementById("toast-template");
        if (!region || !template) {
            return;
        }

        if (activeToast) {
            activeToast.remove();
            activeToast = null;
        }

        const toast = template.content.firstElementChild.cloneNode(true);
        toast.classList.toggle("toast-error", kind === "error");
        toast.querySelector("[data-toast-message]").textContent = message;
        region.append(toast);
        manageToast(toast, kind === "error" ? 8000 : 5000);
    }

    function setupInitialToast() {
        const toast = region?.querySelector("[data-toast]");
        if (!toast) {
            return;
        }

        const message = toast.querySelector("[data-toast-message]");
        const text = message.textContent;
        message.textContent = "";
        setTimeout(() => {
            message.textContent = text;
        }, 100);

        manageToast(toast, 5000);
        popCartCount();
    }

    function popCartCount() {
        const count = document.getElementById("cart-count");
        if (count && !count.hidden && animate) {
            gsap.fromTo(count, { scale: 0.3 }, { scale: 1, duration: 0.6, ease: "back.out(3)" });
        }
    }

    function setCartCount(count) {
        const badge = document.getElementById("cart-count");
        const text = document.getElementById("cart-count-text");
        if (!badge || !text) {
            return;
        }

        badge.hidden = count === 0;
        badge.textContent = count === 0 ? "" : String(count);
        text.textContent = count === 0 ? "" : count === 1 ? text.dataset.one : text.dataset.many.replace("{0}", String(count));
        popCartCount();
    }

    function visible(element) {
        if (!element) {
            return false;
        }

        const box = element.getBoundingClientRect();
        return box.width > 0 && box.height > 0;
    }

    function flyToCart(from, color) {
        const target = [document.getElementById("cart-link"), document.querySelector("label[for=menu-toggle]")].find(visible);
        if (!animate || !target) {
            return Promise.resolve();
        }

        const start = from.getBoundingClientRect();
        const end = target.getBoundingClientRect();
        const cap = document.createElement("span");
        cap.className = "fly-cap";
        cap.style.backgroundColor = color || getComputedStyle(root).getPropertyValue("--brand");
        cap.style.left = `${start.left + (start.width / 2)}px`;
        cap.style.top = `${start.top + (start.height / 2)}px`;
        document.body.append(cap);

        const dx = (end.left + (end.width / 2)) - (start.left + (start.width / 2));
        const dy = (end.top + (end.height / 2)) - (start.top + (start.height / 2));

        return new Promise((resolve) => {
            gsap.timeline({ onComplete: () => { cap.remove(); resolve(); } })
                .to(cap, { x: dx, duration: 0.7, ease: "power1.inOut" }, 0)
                .to(cap, { y: dy, duration: 0.7, ease: "back.in(1.6)" }, 0)
                .to(cap, { rotation: 180, scale: 0.5, duration: 0.7, ease: "power1.in" }, 0);
        });
    }

    window.keycapStore = {
        animate,
        showToast,
        setCartCount,
        flyToCart,
        messages: {
            added: region?.dataset.added ?? "",
            failed: region?.dataset.failed ?? "",
        },
    };

    document.addEventListener("DOMContentLoaded", function () {
        revealItems();
        setupInitialToast();
        setupSpotlight();
        setupKeycaps();
        setupTyping();
        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape" && activeToast) {
                closeToast(activeToast);
            }
        });
    });
})();
