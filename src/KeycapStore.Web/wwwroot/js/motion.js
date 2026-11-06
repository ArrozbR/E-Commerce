(function () {
    const root = document.documentElement;
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const gsap = window.gsap;
    const animate = Boolean(gsap) && !reduced;

    function fallbackStarted(items) {
        if (!document.getAnimations) {
            return performance.now() > 1000;
        }

        return [...items].some((item) => item.getAnimations().some((animation) =>
            animation.animationName === "reveal-fallback" && (animation.currentTime ?? 0) > 1100));
    }

    function revealItems() {
        if (!root.classList.contains("motion")) {
            return;
        }

        const items = document.querySelectorAll("[data-reveal]");
        if (!animate || fallbackStarted(items)) {
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

    function setupSpotlight(container) {
        if (reduced || !window.matchMedia("(pointer: fine)").matches) {
            return;
        }

        (container ?? document).querySelectorAll(".card-interactive").forEach((card) => {
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

        if (!reduced && !stage.hasAttribute("data-showcase")) {
            stage.querySelectorAll(".keycap").forEach((key) => {
                const delay = 500 + ((parseFloat(getComputedStyle(key).getPropertyValue("--d")) || 0) * 600);
                setTimeout(() => {
                    pressKey(key);
                    releaseKey(key);
                }, delay);
            });
        }
    }

    function readableOn(hex) {
        const channel = (start) => {
            const value = parseInt(hex.slice(start, start + 2), 16) / 255;
            return value <= 0.03928 ? value / 12.92 : Math.pow((value + 0.055) / 1.055, 2.4);
        };
        const luminance = (0.2126 * channel(1)) + (0.7152 * channel(3)) + (0.0722 * channel(5));
        const contrast = (a, b) => (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);

        return contrast(luminance, 0.0030) >= contrast(luminance, 1) ? "#0a0a0a" : "#ffffff";
    }

    function setupShowcase() {
        const showcase = document.querySelector("[data-showcase]");
        if (!showcase || reduced) {
            return;
        }

        const screen = showcase.querySelector("[data-screen]");
        const kits = [...showcase.querySelectorAll("[data-showcase-kit]")];
        const words = (showcase.dataset.words || "").split("|").filter(Boolean);
        let kitIndex = 0;
        let inView = true;
        let userUntil = 0;

        const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
        const userTyping = () => performance.now() < userUntil;
        const keyName = (char) => (char === " " ? "Space" : char.toUpperCase());

        function tap(name) {
            const key = showcase.querySelector(`.keycap[data-key="${CSS.escape(name)}"]`);
            if (key) {
                pressKey(key);
                releaseKey(key);
            }
        }

        function recolor(kit) {
            const caps = { Alpha: kit.dataset.keys, Modifier: kit.dataset.legends, Accent: kit.dataset.accent };
            const legends = { Alpha: kit.dataset.legends, Modifier: kit.dataset.keys, Accent: readableOn(kit.dataset.accent) };

            showcase.querySelectorAll(".keycap").forEach((key) => {
                const role = key.dataset.role;
                const parts = [[key.querySelectorAll(".keycap-cap"), caps[role]], [key.querySelectorAll(".keycap-legend"), legends[role]]];
                parts.forEach(([elements, color]) => {
                    if (animate) {
                        gsap.to(elements, { fill: color, duration: 0.7, ease: "power2.out" });
                    } else {
                        elements.forEach((element) => element.style.setProperty("fill", color));
                    }
                });
            });
        }

        function nextKit() {
            if (kits.length < 2) {
                return;
            }

            kitIndex = (kitIndex + 1) % kits.length;
            kits.forEach((kit, index) => {
                kit.hidden = index !== kitIndex;
            });
            recolor(kits[kitIndex]);
        }

        async function typeWord(word) {
            for (const char of word) {
                if (userTyping()) {
                    return false;
                }

                tap(keyName(char));
                screen.textContent += char;
                await sleep(110 + (Math.random() * 110));
            }

            await sleep(1500);
            while (screen.textContent.length > 0) {
                if (userTyping()) {
                    return false;
                }

                tap("Backspace");
                screen.textContent = screen.textContent.slice(0, -1);
                await sleep(55);
            }

            return true;
        }

        async function loop() {
            let index = 0;
            await sleep(900);
            screen.textContent = "";
            for (;;) {
                while (userTyping() || !inView || document.hidden) {
                    await sleep(300);
                }

                if (userUntil !== 0) {
                    userUntil = 0;
                    screen.textContent = "";
                }

                if (await typeWord(words[index % words.length])) {
                    index++;
                    nextKit();
                    await sleep(600);
                }
            }
        }

        new IntersectionObserver(([entry]) => {
            inView = entry.isIntersecting;
        }).observe(showcase);

        document.addEventListener("keydown", (event) => {
            if (!inView || event.ctrlKey || event.metaKey || event.altKey || (event.target instanceof Element && event.target.closest("input, textarea, select, [contenteditable]"))) {
                return;
            }

            if (event.key.length === 1 || event.key === "Backspace") {
                if (!userTyping()) {
                    screen.textContent = "";
                }

                userUntil = performance.now() + 3500;
                screen.textContent = event.key === "Backspace" ? screen.textContent.slice(0, -1) : (screen.textContent + event.key).slice(-20);
            }
        });

        if (words.length > 0) {
            loop();
        }
    }

    function setupSteps() {
        const steps = document.querySelector("[data-steps]");
        const line = steps?.querySelector("[data-steps-line]");
        if (!line || !animate || !window.ScrollTrigger) {
            return;
        }

        gsap.registerPlugin(window.ScrollTrigger);
        gsap.fromTo(line, { scaleX: 0 }, {
            scaleX: 1,
            duration: 1.4,
            ease: "power2.inOut",
            scrollTrigger: { trigger: steps, start: "top 80%", once: true },
        });
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

    function enhance(container) {
        const items = container.querySelectorAll("[data-reveal]");
        if (animate && root.classList.contains("motion-ready")) {
            gsap.fromTo(items, { opacity: 0, y: 20 }, { opacity: 1, y: 0, duration: 0.45, ease: "power3.out", stagger: 0.05 });
        } else {
            items.forEach((item) => {
                item.style.opacity = "1";
            });
        }

        setupSpotlight(container);
    }

    window.keycapStore = {
        animate,
        enhance,
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
        setupSpotlight(document);
        setupKeycaps();
        setupTyping();
        setupShowcase();
        setupSteps();
        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape" && activeToast) {
                closeToast(activeToast);
            }
        });
    });
})();
