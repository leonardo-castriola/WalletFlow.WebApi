const chartOrderKey = "walletflow.dashboard.chart-order";
const transactionUpdateListeners = new Map();
let nextTransactionUpdateListenerId = 1;

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

export function subscribeToTransactionUpdates(dotNetReference) {
    const id = nextTransactionUpdateListenerId++;
    const listener = () => {
        dotNetReference.invokeMethodAsync("AtualizarGraficosAsync")
            .catch(() => { /* A tela pode estar sendo fechada durante a atualização. */ });
    };
    document.addEventListener("walletflow:transacoes-atualizadas", listener);
    transactionUpdateListeners.set(id, listener);
    return id;
}

export function unsubscribeFromTransactionUpdates(id) {
    const listener = transactionUpdateListeners.get(id);
    if (!listener) return;
    document.removeEventListener("walletflow:transacoes-atualizadas", listener);
    transactionUpdateListeners.delete(id);
}
