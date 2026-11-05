(function () {
    const container = document.querySelector("[data-order-return]");
    if (!container) {
        return;
    }

    const statusText = document.getElementById("order-status");
    const waiting = document.getElementById("order-waiting");
    const icon = container.querySelector("[data-thanks-key]");
    const gsap = window.gsap;
    const animate = Boolean(gsap) && !window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const colors = ["#f97316", "#22c55e", "#a5f3fc", "#fbbf24", "#ef4444", "#f0abfc", "#fef3c7"];
    let attempts = 0;
    let celebrated = false;

    function burst() {
        const box = container.getBoundingClientRect();
        const center = icon.getBoundingClientRect();
        const x = center.left - box.left + (center.width / 2);
        const y = center.top - box.top + (center.height / 2);

        for (let i = 0; i < 36; i++) {
            const piece = document.createElement("span");
            piece.className = "confetti";
            piece.style.left = `${x}px`;
            piece.style.top = `${y}px`;
            piece.style.backgroundColor = colors[i % colors.length];
            container.append(piece);

            const angle = Math.random() * Math.PI * 2;
            const distance = 90 + (Math.random() * 170);
            gsap.timeline({ onComplete: () => piece.remove() })
                .to(piece, { x: Math.cos(angle) * distance, y: (Math.sin(angle) * distance) - 40, rotation: (Math.random() * 540) - 270, duration: 0.9, ease: "power3.out" })
                .to(piece, { y: "+=90", opacity: 0, duration: 0.8, ease: "power1.in" }, ">-0.2");
        }
    }

    function celebrate() {
        if (celebrated) {
            return;
        }

        celebrated = true;
        icon.classList.add("is-confirmed");
        if (animate) {
            gsap.fromTo(icon, { scale: 0.5, rotation: -14 }, { scale: 1, rotation: 0, duration: 0.8, ease: "back.out(3)" });
            burst();
        }
    }

    async function check() {
        attempts++;
        try {
            const response = await fetch(container.dataset.statusUrl, { headers: { Accept: "application/json" } });
            if (response.ok) {
                const order = await response.json();
                statusText.textContent = order.text;
                statusText.className = `badge badge-${order.tone} px-3 py-1 text-sm`;
                if (order.status !== "AwaitingPayment") {
                    waiting.textContent = container.dataset.confirmedText;
                    if (order.tone === "success") {
                        celebrate();
                    }
                    return;
                }
            }
        } catch {
        }

        if (attempts < 30) {
            setTimeout(check, 2000);
        } else {
            waiting.textContent = container.dataset.slowText;
        }
    }

    check();
})();
