(() => {
    let connection = null;
    let starting = false;
    let hideTimer = null;
    let activeAlertText = null;
    let activeAlertUntil = 0;
    let lastAlertSignature = null;
    let lastAlertAt = 0;

    function applyToastToDom() {
        const toast = document.getElementById('budget-alert-toast');
        if (!toast) return;

        if (activeAlertText && Date.now() < activeAlertUntil) {
            if (toast.textContent !== activeAlertText)
                toast.textContent = activeAlertText;
            if (toast.hidden) {
                toast.hidden = false;
                toast.removeAttribute('hidden');
            }
        }
    }

    function showBudgetAlert(alerta) {
        if (!alerta) return;

        const year = alerta.ano ?? alerta.Ano ?? '';
        const month = alerta.mes ?? alerta.Mes ?? '';
        const percent = Number(alerta.percentualUtilizado ?? alerta.PercentualUtilizado ?? 100);
        const expenses = Number(alerta.despesas ?? alerta.Despesas ?? 0);
        const limit = Number(alerta.limite ?? alerta.Limite ?? 0);
        const signature = `${year}-${month}-${limit}-${expenses}-${percent}`;
        const now = Date.now();
        if (signature === lastAlertSignature && now - lastAlertAt < 4000) {
            applyToastToDom();
            return;
        }

        lastAlertSignature = signature;
        lastAlertAt = now;

        const money = value => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value);
        activeAlertText = 'Atenção: você utilizou ' + percent.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 }) + '% do orçamento deste mês (' + money(expenses) + ' de ' + money(limit) + ').';
        activeAlertUntil = now + 10000;
        applyToastToDom();

        clearTimeout(hideTimer);
        hideTimer = setTimeout(() => {
            activeAlertText = null;
            activeAlertUntil = 0;
            const toast = document.getElementById('budget-alert-toast');
            if (toast) toast.hidden = true;
        }, 10000);
    }

    window.walletFlowShowBudgetAlert = showBudgetAlert;

    function ensureBudgetAlertsConnection() {
        applyToastToDom();

        const toast = document.getElementById('budget-alert-toast');
        if (!toast) {
            if (connection) {
                const current = connection;
                connection = null;
                starting = false;
                current.stop().catch(() => {});
            }
            return;
        }

        if (!window.signalR || connection || starting)
            return;

        starting = true;
        const hubConnection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/alertas-orcamento')
            .withAutomaticReconnect()
            .build();

        hubConnection.on('AlertaOrcamento', showBudgetAlert);

        hubConnection.on('TransacoesAtualizadas', () => {
            document.dispatchEvent(new CustomEvent('walletflow:transacoes-atualizadas'));
        });

        hubConnection.onclose(() => {
            if (connection === hubConnection)
                connection = null;
        });

        hubConnection.start()
            .then(() => {
                connection = hubConnection;
                starting = false;
            })
            .catch(error => {
                starting = false;
                console.warn('Não foi possível conectar aos alertas de orçamento em tempo real.', error);
            });
    }

    function initializeBudgetAlerts() {
        ensureBudgetAlertsConnection();
        if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
            window.Blazor.addEventListener('enhancedload', ensureBudgetAlertsConnection);
        }
        if (document.body && typeof MutationObserver !== 'undefined') {
            const observer = new MutationObserver(() => ensureBudgetAlertsConnection());
            observer.observe(document.body, { childList: true, subtree: true });
        }
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', initializeBudgetAlerts, { once: true });
    else
        initializeBudgetAlerts();
})();
