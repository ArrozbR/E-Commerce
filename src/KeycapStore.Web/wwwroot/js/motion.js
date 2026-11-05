(function () {
    const root = document.documentElement;
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const gsap = window.gsap;

    function revealItems() {
        if (!root.classList.contains("motion")) {
            return;
        }

        const items = document.querySelectorAll("[data-reveal]");
        if (!gsap || reduced || performance.now() > 1000) {
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

    function popCartCount() {
        const count = document.getElementById("cart-count");
        if (count && gsap && !reduced) {
            gsap.fromTo(count, { scale: 0.3 }, { scale: 1, duration: 0.6, ease: "back.out(3)", delay: 0.2 });
        }
    }

    function setupToast() {
        const toast = document.querySelector("[data-toast]");
        if (!toast) {
            return;
        }

        toast.style.animation = "none";

        const message = toast.querySelector("[data-toast-message]");
        const text = message.textContent;
        message.textContent = "";
        setTimeout(() => {
            message.textContent = text;
        }, 100);

        let timer;
        let closed = false;

        function close() {
            if (closed) {
                return;
            }

            closed = true;
            clearTimeout(timer);
            if (gsap && !reduced) {
                gsap.to(toast, { opacity: 0, y: 16, duration: 0.25, ease: "power2.in", onComplete: () => toast.remove() });
            } else {
                toast.remove();
            }
        }

        function schedule() {
            clearTimeout(timer);
            timer = setTimeout(close, 5000);
        }

        toast.querySelector("[data-toast-close]")?.addEventListener("click", close);
        toast.addEventListener("mouseenter", () => clearTimeout(timer));
        toast.addEventListener("mouseleave", schedule);
        toast.addEventListener("focusin", () => clearTimeout(timer));
        toast.addEventListener("focusout", schedule);
        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                close();
            }
        });

        if (gsap && !reduced) {
            gsap.from(toast, { opacity: 0, y: 24, scale: 0.96, duration: 0.45, ease: "back.out(2)" });
        }

        schedule();
        popCartCount();
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

    document.addEventListener("DOMContentLoaded", function () {
        revealItems();
        setupToast();
        setupSpotlight();
    });
})();
