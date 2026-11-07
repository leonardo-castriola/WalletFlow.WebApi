(() => {
    function startBudgetAlerts() {
        const toast = document.getElementById('budget-alert-toast');
        if (!toast || !window.signalR) return;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/alertas-orcamento')
            .withAutomaticReconnect()
            .build();

        let hideTimer;
        connection.on('AlertaOrcamento', alerta => {
            const percent = Number(alerta.percentualUtilizado ?? alerta.PercentualUtilizado ?? 80);
            const expenses = Number(alerta.despesas ?? alerta.Despesas ?? 0);
            const limit = Number(alerta.limite ?? alerta.Limite ?? 0);
            const money = value => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value);
            toast.textContent = 'Atenção: você utilizou ' + percent.toLocaleString('pt-BR') + '% do orçamento deste mês (' + money(expenses) + ' de ' + money(limit) + ').';
            toast.hidden = false;
            clearTimeout(hideTimer);
            hideTimer = setTimeout(() => { toast.hidden = true; }, 10000);
        });

        connection.on('TransacoesAtualizadas', () => {
            document.dispatchEvent(new CustomEvent('walletflow:transacoes-atualizadas'));
        });

        connection.start().catch(error => console.warn('Não foi possível conectar aos alertas de orçamento em tempo real.', error));
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', startBudgetAlerts, { once: true });
    else
        startBudgetAlerts();
})();
