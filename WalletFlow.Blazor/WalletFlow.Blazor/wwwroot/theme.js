(() => {
    const storageKey = 'walletflow-theme';

    function readPreference() {
        const cookie = document.cookie.split(';').map(value => value.trim())
            .find(value => value.startsWith(`${storageKey}=`));
        const cookieTheme = cookie?.slice(storageKey.length + 1);
        if (cookieTheme === 'dark' || cookieTheme === 'light') return cookieTheme;

        try {
            const saved = localStorage.getItem(storageKey);
            if (saved === 'dark' || saved === 'light') return saved;
        } catch { }
        return 'light';
    }

    function savePreference(theme) {
        try { localStorage.setItem(storageKey, theme); } catch { }
        document.cookie = `${storageKey}=${theme}; path=/; max-age=31536000; samesite=lax`;
    }

    function updateButtons(isDark) {
        document.querySelectorAll('.theme-toggle').forEach(button => {
            const label = isDark ? 'Ativar tema claro' : 'Ativar tema escuro';
            if (button.getAttribute('aria-label') !== label) button.setAttribute('aria-label', label);
            if (button.title !== label) button.title = label;
            const icon = button.querySelector('[aria-hidden="true"]');
            const iconText = isDark ? '☀' : '☾';
            if (icon && icon.textContent !== iconText) icon.textContent = iconText;
        });
    }

    function apply(theme) {
        const isDark = theme === 'dark';
        document.documentElement.dataset.theme = isDark ? 'dark' : 'light';
        updateButtons(isDark);
    }

    window.walletFlowTheme = {
        toggle() {
            const nextTheme = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
            apply(nextTheme);
            savePreference(nextTheme);
        }
    };

    // Aplica a mesma preferência em todas as rotas e sincroniza outras abas abertas.
    apply(readPreference());
    window.addEventListener('storage', event => {
        if (event.key === storageKey) {
            const theme = event.newValue === 'dark' ? 'dark' : 'light';
            apply(theme);
            document.cookie = `${storageKey}=${theme}; path=/; max-age=31536000; samesite=lax`;
        }
    });
    window.addEventListener('pageshow', () => apply(readPreference()));
    document.addEventListener('enhancedload', () => apply(readPreference()));

    // O controle pode ser renderizado depois pelo AuthorizeView do Blazor.
    new MutationObserver(() => updateButtons(document.documentElement.dataset.theme === 'dark'))
        .observe(document.documentElement, { childList: true, subtree: true });
})();
