(() => {
    const forms = [
        {
            cep: "register-cep",
            street: "register-street",
            neighborhood: "register-neighborhood",
            city: "register-city",
            state: "register-state",
            status: "register-cep-status"
        },
        {
            cep: "profile-cep",
            street: "profile-street",
            neighborhood: "profile-neighborhood",
            city: "profile-city",
            state: "profile-state",
            status: "profile-cep-status"
        }
    ];

    const fieldsByCepId = new Map(forms.map(fields => [fields.cep, fields]));
    const states = new Map();

    function updateField(id, value) {
        const input = document.getElementById(id);
        if (!input || !value) return;
        input.value = value;
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function setStatus(fields, text) {
        const status = document.getElementById(fields.status);
        if (status) status.textContent = text;
    }

    async function lookup(fields, input, state, cep, requestId) {
        setStatus(fields, "Buscando endereço...");
        try {
            const response = await fetch(`https://viacep.com.br/ws/${cep}/json/`, {
                headers: { Accept: "application/json" }
            });
            if (!response.ok) throw new Error("Falha na consulta");
            const address = await response.json();
            if (requestId !== state.requestId || input.value.replace(/\D/g, "") !== cep) return;
            if (address.erro) {
                setStatus(fields, "CEP não encontrado. Confira o número ou preencha o endereço manualmente.");
                return;
            }

            updateField(fields.street, address.logradouro);
            updateField(fields.neighborhood, address.bairro);
            updateField(fields.city, address.localidade);
            updateField(fields.state, address.uf);
            setStatus(fields, "Endereço preenchido. Confira os dados e complete o número.");
        } catch {
            if (requestId === state.requestId) {
                setStatus(fields, "Não foi possível consultar o CEP. Você pode preencher o endereço manualmente.");
            }
        }
    }

    document.addEventListener("input", event => {
        const input = event.target;
        const fields = input instanceof HTMLElement ? fieldsByCepId.get(input.id) : null;
        if (!fields) return;

        let state = states.get(fields.cep);
        if (!state) {
            state = { requestId: 0, timer: null };
            states.set(fields.cep, state);
        }

        clearTimeout(state.timer);
        const cep = input.value.replace(/\D/g, "");
        if (cep.length !== 8) {
            state.requestId++;
            setStatus(fields, "Digite os 8 números do CEP para buscar o endereço.");
            return;
        }

        const requestId = ++state.requestId;
        state.timer = setTimeout(() => lookup(fields, input, state, cep, requestId), 350);
    });
})();
