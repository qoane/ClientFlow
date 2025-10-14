const API = {
    async getSurvey(code) { const r = await fetch(`/api/surveys/${encodeURIComponent(code)}`); if (!r.ok) throw new Error('survey not found'); return r.json(); },
    async submit(code, data) { const r = await fetch(`/api/surveys/${encodeURIComponent(code)}/responses`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ data }) }); if (!r.ok) throw new Error('submit failed'); return r.json(); },
    async nps(code) { const r = await fetch(`/api/surveys/${encodeURIComponent(code)}/nps`); return r.json(); },
    async adminList() { const r = await fetch('/api/admin/surveys'); return r.json(); },
    async toggle(id, active) { const r = await fetch(`/api/admin/surveys/${id}/toggle?active=${active}`, { method: 'POST' }); return r.ok; }
};