const chartOrderKey = "walletflow.dashboard.chart-order";

export function loadChartOrder() {
    try {
        const saved = localStorage.getItem(chartOrderKey);
        return saved ? JSON.parse(saved) : [];
    } catch {
        return [];
    }
}

export function saveChartOrder(order) {
    try {
        localStorage.setItem(chartOrderKey, JSON.stringify(order));
    } catch {
        // The dashboard still reorders for the current visit if storage is unavailable.
    }
}
