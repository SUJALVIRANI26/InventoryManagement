document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-add-row]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const target = document.querySelector(btn.getAttribute("data-add-row"));
            const row = target.querySelector("[data-line-row]");
            const clone = row.cloneNode(true);
            clone.querySelectorAll("input, select").forEach((el) => {
                if (el.tagName === "INPUT") {
                    if (el.type === "number") {
                        el.value = el.name.includes("Quantity") ? "1" : "0";
                    } else {
                        el.value = "";
                    }
                }
            });
            reindex(target);
            target.appendChild(clone);
            reindex(target);
        });
    });

    document.querySelectorAll("[data-chart]").forEach((canvas) => initChart(canvas));
});

function reindex(container) {
    const rows = [...container.querySelectorAll("[data-line-row]")];
    rows.forEach((row, index) => {
        row.querySelectorAll("input, select").forEach((el) => {
            el.name = el.name.replace(/Lines\[\d+\]/, `Lines[${index}]`);
        });
    });
}

async function initChart(canvas) {
    const urlBase = canvas.dataset.url;
    const type = canvas.dataset.type || "";
    const buttons = document.querySelectorAll(canvas.dataset.buttons || ".period-btn");
    let chart;

    async function load(period) {
        const url = new URL(urlBase, window.location.origin);
        if (type) url.searchParams.set("type", type);
        url.searchParams.set("period", period);
        const res = await fetch(url);
        const data = await res.json();
        const config = {
            type: "line",
            data: {
                labels: data.labels,
                datasets: [{
                    label: canvas.dataset.label || "Value",
                    data: data.values,
                    borderColor: "#0f766e",
                    backgroundColor: "rgba(15, 118, 110, 0.15)",
                    fill: true,
                    tension: 0.3
                }]
            },
            options: {
                responsive: true,
                plugins: { legend: { display: true } },
                scales: { y: { beginAtZero: true } }
            }
        };
        if (chart) {
            chart.data = config.data;
            chart.update();
        } else {
            chart = new Chart(canvas, config);
        }
    }

    buttons.forEach((btn) => {
        btn.addEventListener("click", () => {
            buttons.forEach((b) => b.classList.remove("active"));
            btn.classList.add("active");
            load(btn.dataset.period);
        });
    });

    const initial = document.querySelector(`${canvas.dataset.buttons || ".period-btn"}.active`)?.dataset.period || "daily";
    await load(initial);
}
