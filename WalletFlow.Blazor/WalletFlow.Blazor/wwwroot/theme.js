(() => {
    const storageKey = 'walletflow-theme';

    // O arquivo pode ser avaliado novamente durante navegações aprimoradas.
    // Mantém um único conjunto de listeners para que um clique alterne o tema uma só vez.
    if (window.walletFlowTheme?.initialized) {
        window.walletFlowTheme.sync();
        return;
    }

    function readPreference() {
        // localStorage é por origem e funciona igualmente em todas as rotas e
        // após o login/logout. Ele evita que cookies antigos por caminho
        // sobrescrevam a preferência atual ao abrir outra página.
        try {
            const saved = localStorage.getItem(storageKey);
            if (saved === 'dark' || saved === 'light') return saved;
        } catch { }

        // O cookie mantém um fallback caso o navegador bloqueie localStorage.
        try {
            const cookies = document.cookie.split(';').map(value => value.trim())
                .filter(value => value.startsWith(`${storageKey}=`));
            // Cookies com caminho mais específico vêm primeiro; o último é
            // normalmente o cookie de escopo global (path=/).
            const cookieTheme = cookies.at(-1)?.slice(storageKey.length + 1);
            if (cookieTheme === 'dark' || cookieTheme === 'light') return cookieTheme;
        } catch { }

        return 'light';
    }

    function savePreference(theme) {
        try { localStorage.setItem(storageKey, theme); } catch { }
        try { document.cookie = `${storageKey}=${theme}; path=/; max-age=31536000; samesite=lax`; } catch { }
    }

    function updateButtons(isDark) {
        document.querySelectorAll('.theme-toggle').forEach(button => {
            const label = isDark ? 'Ativar tema claro' : 'Ativar tema escuro';
            if (button.getAttribute('aria-label') !== label) button.setAttribute('aria-label', label);
            if (button.title !== label) button.title = label;
            button.setAttribute('aria-pressed', isDark ? 'true' : 'false');
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

    function sync() {
        const savedTheme = readPreference();
        const currentTheme = document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light';
        if (currentTheme !== savedTheme) apply(savedTheme);
        else updateButtons(savedTheme === 'dark');
    }

    window.walletFlowTheme = {
        initialized: true,
        sync,
        toggle() {
            const nextTheme = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
            apply(nextTheme);
            savePreference(nextTheme);
        },
        set(theme) {
            if (theme !== 'dark' && theme !== 'light') return;
            apply(theme);
            savePreference(theme);
        }
    };

    // Um único controle no layout atende a tela pública e todas as páginas autenticadas.
    document.addEventListener('click', event => {
        const button = event.target instanceof Element
            ? event.target.closest('[data-theme-toggle]')
            : null;
        if (button) window.walletFlowTheme.toggle();
    });

    // Aplica a mesma preferência em todas as rotas e sincroniza outras abas abertas.
    apply(readPreference());
    window.addEventListener('storage', event => {
        if (event.key === storageKey) {
            const theme = event.newValue === 'dark' ? 'dark' : 'light';
            apply(theme);
            try { document.cookie = `${storageKey}=${theme}; path=/; max-age=31536000; samesite=lax`; } catch { }
        }
    });
    window.addEventListener('pageshow', sync);
    window.addEventListener('focus', sync);
    document.addEventListener('enhancedload', sync);

    // Blazor pode substituir partes do DOM ou restaurar atributos durante a navegação.
    // Reaplica a preferência salva e sincroniza o botão depois dessas atualizações.
    new MutationObserver(sync).observe(document.documentElement, {
        attributes: true,
        attributeFilter: ['data-theme'],
        childList: true,
        subtree: true
    });
})();
