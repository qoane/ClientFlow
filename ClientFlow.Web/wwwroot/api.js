const __fetchApi = typeof window !== 'undefined' && typeof window.appApiFetch === 'function'
    ? window.appApiFetch
    : (path, options) => fetch(path, options);

const API = {
    async getSurvey(code) {
        const r = await __fetchApi(`api/surveys/${encodeURIComponent(code)}`);
        if (!r.ok) throw new Error('survey not found');
        return r.json();
    },
    async getSurveyDefinition(code) {
        const r = await __fetchApi(`api/surveys/${encodeURIComponent(code)}/definition`);
        if (!r.ok) throw new Error('survey definition not found');
        return r.json();
    },
    async submit(code, data) {
        const r = await __fetchApi(`api/surveys/${encodeURIComponent(code)}/submit`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ answers: data })
        });
        if (!r.ok) throw new Error('submit failed');
        return r.json();
    },
    async nps(code) {
        const r = await __fetchApi(`api/surveys/${encodeURIComponent(code)}/nps`);
        return r.json();
    },
    async adminList() {
        const r = await __fetchApi('api/admin/surveys');
        return r.json();
    },
    async toggle(id, active) {
        const r = await __fetchApi(`api/admin/surveys/${id}/toggle?active=${active}`, { method: 'POST' });
        return r.ok;
    }
};
