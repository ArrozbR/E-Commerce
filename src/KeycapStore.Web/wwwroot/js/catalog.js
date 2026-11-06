(function () {
    const form = document.querySelector("[data-catalog-filter]");
    const results = document.getElementById("catalog-results");
    if (!form || !results || !window.fetch || !window.DOMParser) {
        return;
    }

    let controller = null;
    let timer = 0;

    function addressFor() {
        const params = new URLSearchParams(new FormData(form));
        [...params.keys()].forEach((key) => {
            if (params.get(key) === "" || (key === "sort" && params.get(key) === "name")) {
                params.delete(key);
            }
        });

        const query = params.toString();
        return query ? `${form.action}?${query}` : form.action;
    }

    async function update() {
        const address = addressFor();
        controller?.abort();
        controller = new AbortController();

        try {
            const response = await fetch(address, { signal: controller.signal, headers: { Accept: "text/html" } });
            if (!response.ok) {
                window.location.href = address;
                return;
            }

            const page = new DOMParser().parseFromString(await response.text(), "text/html");
            const fresh = page.getElementById("catalog-results");
            if (!fresh) {
                window.location.href = address;
                return;
            }

            results.innerHTML = fresh.innerHTML;
            history.replaceState(null, "", address);
            window.keycapStore?.enhance?.(results);
        } catch (error) {
            if (error.name !== "AbortError") {
                window.location.href = address;
            }
        }
    }

    form.querySelector("[data-catalog-submit]")?.setAttribute("hidden", "");

    form.addEventListener("submit", (event) => {
        event.preventDefault();
        update();
    });

    form.addEventListener("change", (event) => {
        if (event.target.name !== "q") {
            update();
        }
    });

    form.addEventListener("input", (event) => {
        if (event.target.name === "q") {
            clearTimeout(timer);
            timer = setTimeout(update, 300);
        }
    });
})();
