(function () {
    const container = document.getElementById("payment");
    if (!container) {
        return;
    }

    const message = document.getElementById("payment-message");
    const button = document.getElementById("pay-button");
    const root = document.documentElement;

    function startCountdown() {
        const countdown = document.querySelector("[data-countdown]");
        const time = countdown?.querySelector("[data-countdown-time]");
        if (!countdown || !time) {
            return;
        }

        const expires = Date.parse(countdown.dataset.expires);
        const tick = () => {
            const left = Math.max(0, Math.floor((expires - Date.now()) / 1000));
            time.textContent = `${String(Math.floor(left / 60)).padStart(2, "0")}:${String(left % 60).padStart(2, "0")}`;
            countdown.classList.toggle("countdown-late", left > 0 && left <= 300);
            if (left === 0) {
                countdown.classList.add("countdown-over");
                countdown.lastElementChild.textContent = countdown.dataset.expiredText;
                button.disabled = true;
                clearInterval(timer);
            }
        };
        const timer = setInterval(tick, 1000);
        tick();
    }

    function appearance(dark) {
        const colors = dark
            ? { primary: "#f97316", background: "#18181b", text: "#fafafa", danger: "#f87171", line: "#3f3f46", glow: "rgba(249, 115, 22, 0.35)" }
            : { primary: "#c2410c", background: "#ffffff", text: "#0a0a0a", danger: "#b91c1c", line: "#d4d4d8", glow: "rgba(194, 65, 12, 0.25)" };

        return {
            theme: dark ? "night" : "stripe",
            variables: {
                colorPrimary: colors.primary,
                colorBackground: colors.background,
                colorText: colors.text,
                colorDanger: colors.danger,
                fontFamily: "\"DM Sans\", system-ui, sans-serif",
                fontSizeBase: "16px",
                borderRadius: "8px",
            },
            rules: {
                ".Input": { borderColor: colors.line, boxShadow: "none" },
                ".Input:focus": { borderColor: colors.primary, boxShadow: `0 0 0 3px ${colors.glow}` },
                ".Tab": { borderColor: colors.line, boxShadow: "none" },
                ".Tab--selected": { borderColor: colors.primary, boxShadow: `0 0 0 1px ${colors.primary}, 0 0 18px ${colors.glow}` },
            },
        };
    }

    async function startPayment() {
        if (typeof Stripe === "undefined") {
            message.textContent = container.dataset.loadError;
            return;
        }

        const isDark = () => root.classList.contains("dark");
        let dark = isDark();

        const stripe = Stripe(container.dataset.publishableKey, { locale: container.dataset.locale });
        const checkout = stripe.initCheckoutElementsSdk({
            clientSecret: container.dataset.clientSecret,
            elementsOptions: { appearance: appearance(dark) },
        });

        new MutationObserver(() => {
            if (isDark() !== dark) {
                dark = isDark();
                checkout.changeAppearance(appearance(dark));
            }
        }).observe(root, { attributes: true, attributeFilter: ["class"] });

        checkout.on("change", (session) => {
            if (!document.querySelector(".countdown-over")) {
                button.disabled = !session.canConfirm;
            }
        });

        checkout.createPaymentElement().mount("#payment-element");

        button.addEventListener("click", async () => {
            button.disabled = true;
            message.textContent = "";

            const loaded = await checkout.loadActions();
            if (loaded.type !== "success") {
                message.textContent = loaded.error.message;
                button.disabled = false;
                return;
            }

            const result = await loaded.actions.confirm();
            if (result.type === "error") {
                message.textContent = result.error.message;
                button.disabled = false;
            }
        });
    }

    startCountdown();
    startPayment();
})();
