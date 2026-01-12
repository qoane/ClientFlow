export class SurveyDesignerStore {
    constructor(initial = {}) {
        this.state = {
            code: initial.code ?? "",
            title: initial.title ?? "",
            description: initial.description,
            sections: [],
            questions: [],
            rules: [],
            theme: initial.theme ? { ...initial.theme } : undefined,
        };
        this.listeners = new Set();
        if (initial.sections || initial.questions || initial.rules) {
            this.load(initial);
        }
    }

    subscribe(listener) {
        this.listeners.add(listener);
        listener(this.snapshot());
        return () => this.listeners.delete(listener);
    }

    load(data = {}) {
        if (data.code !== undefined) this.state.code = data.code;
        if (data.title !== undefined) this.state.title = data.title;
        if (data.description !== undefined) this.state.description = data.description;
        if (data.theme !== undefined) this.state.theme = data.theme ? { ...data.theme } : undefined;

        if (data.sections) {
            this.state.sections = data.sections.map((sec, index) => ({
                id: sec.id ?? this.newId(),
                title: sec.title?.trim() || `Section ${index + 1}`,
                order: sec.order ?? index + 1,
                columns: sec.columns && sec.columns > 0 ? Math.min(sec.columns, 2) : 1,
            }));
        }

        if (data.questions) {
            this.state.questions = data.questions.map((q, index) => ({
                id: q.id ?? this.newId(),
                sectionId: q.sectionId ?? null,
                type: q.type ?? "text",
                prompt: q.prompt ?? "Untitled question",
                key: q.key ?? this.makeKey(q.type ?? "q", index),
                required: q.required ?? false,
                order: q.order ?? index + 1,
                settings: this.mergeSettings(this.defaultSettings(q.type ?? "text"), q.settings ?? {}),
                choices: (q.choices ?? []).map((choice, ci) => ({
                    id: choice.id ?? this.newId(),
                    value: choice.value ?? choice.label ?? `choice_${ci + 1}`,
                    label: choice.label ?? choice.value ?? `Choice ${ci + 1}`,
                })),
                validations: Array.isArray(q.validations) ? [...q.validations] : [],
                visibleIf: (() => {
                    const raw = (q?.visibleIf ?? q?.VisibleIf ?? q?.visibility);
                    return typeof raw === "string" ? raw : "";
                })(),
            }));
        }

        if (data.rules) {
            this.state.rules = data.rules.map(rule => ({
                id: rule.id ?? this.newId(),
                sourceQuestionId: rule.sourceQuestionId,
                condition: rule.condition,
                action: rule.action,
            }));
        }

        this.ensureSectionOrder();
        this.ensureAllQuestionOrder();
        this.emit();
    }

    setMeta(meta = {}) {
        if (meta.code !== undefined) this.state.code = meta.code;
        if (meta.title !== undefined) this.state.title = meta.title;
        if (meta.description !== undefined) this.state.description = meta.description;
        this.emit();
    }

    setTheme(theme) {
        this.state.theme = theme ? { ...theme } : undefined;
        this.emit();
    }

    getState() {
        return this.snapshot();
    }

    addSection(title = "New Section", columns = 1) {
        const section = {
            id: this.newId(),
            title: title.trim() || "New Section",
            columns: columns <= 0 ? 1 : Math.min(columns, 2),
            order: this.state.sections.length + 1,
        };
        this.state.sections.push(section);
        this.emit();
        return section;
    }

    updateSection(id, patch = {}) {
        const section = this.state.sections.find(sec => sec.id === id);
        if (!section) return;
        if (patch.title !== undefined) section.title = patch.title.trim() || section.title;
        if (patch.columns !== undefined) section.columns = patch.columns <= 0 ? 1 : Math.min(patch.columns, 2);
        this.emit();
    }

    moveSection(id, newIndex) {
        const sections = this.state.sections;
        const idx = sections.findIndex(sec => sec.id === id);
        if (idx === -1) return;
        const [section] = sections.splice(idx, 1);
        newIndex = Math.max(0, Math.min(newIndex, sections.length));
        sections.splice(newIndex, 0, section);
        this.ensureSectionOrder();
        this.emit();
    }

    deleteSection(id) {
        this.state.sections = this.state.sections.filter(sec => sec.id !== id);
        this.state.questions = this.state.questions.filter(q => q.sectionId !== id);
        this.ensureSectionOrder();
        this.ensureAllQuestionOrder();
        this.emit();
    }

    addQuestion(sectionId, type, input = {}) {
        const question = {
            id: input.id ?? this.newId(),
            sectionId: sectionId ?? null,
            type,
            prompt: input.prompt ?? this.defaultPrompt(type),
            key: input.key ?? this.defaultKey(type, this.state.questions.length + 1),
            required: input.required ?? false,
            order: 0,
            settings: this.mergeSettings(this.defaultSettings(type), input.settings ?? {}),
            choices: input.choices ? input.choices.map(choice => ({
                id: choice.id ?? this.newId(),
                value: choice.value ?? choice.label ?? this.makeKey("choice", 1),
                label: choice.label ?? choice.value ?? "Choice",
            })) : this.defaultChoices(type),
            validations: input.validations ? [...input.validations] : [],
            visibleIf: input.visibleIf ?? "",
        };
        if (this.isStaticType(type)) {
            question.required = false;
        }

        this.state.questions.push(question);
        this.ensureQuestionOrder(sectionId ?? null);
        this.emit();
        return question;
    }

    updateQuestion(id, patch = {}) {
        const question = this.state.questions.find(q => q.id === id);
        if (!question) return;
        if (patch.sectionId !== undefined) question.sectionId = patch.sectionId;
        if (patch.type !== undefined) {
            question.type = patch.type;
            question.settings = this.mergeSettings(this.defaultSettings(question.type), question.settings ?? {});
        }
        if (patch.prompt !== undefined) question.prompt = patch.prompt;
        if (patch.key !== undefined) question.key = patch.key;
        if (patch.required !== undefined) question.required = patch.required;
        if (patch.settings !== undefined) {
            question.settings = this.mergeSettings({}, patch.settings);
        }
        if (patch.choices !== undefined) {
            question.choices = patch.choices.map(choice => ({
                id: choice.id ?? this.newId(),
                value: choice.value ?? choice.label ?? this.makeKey("choice", 1),
                label: choice.label ?? choice.value ?? "Choice",
            }));
        }
        if (patch.validations !== undefined) question.validations = [...patch.validations];
        if (patch.visibleIf !== undefined) question.visibleIf = patch.visibleIf;
        if (this.isStaticType(question.type)) {
            question.required = false;
        }
        if (patch.order !== undefined) question.order = patch.order;
        this.ensureAllQuestionOrder();
        this.emit();
    }

    deleteQuestion(id) {
        const question = this.state.questions.find(q => q.id === id);
        if (!question) return;
        const sectionId = question.sectionId ?? null;
        this.state.questions = this.state.questions.filter(q => q.id !== id);
        this.ensureQuestionOrder(sectionId);
        this.emit();
    }

    moveQuestion(id, targetSectionId, index) {
        const question = this.state.questions.find(q => q.id === id);
        if (!question) return;
        const sourceSection = question.sectionId ?? null;
        const target = targetSectionId ?? null;
        question.sectionId = target;

        const targetQuestions = this.state.questions
            .filter(q => (q.sectionId ?? null) === target && q.id !== id)
            .sort((a, b) => a.order - b.order);
        index = Math.max(0, Math.min(index, targetQuestions.length));
        targetQuestions.splice(index, 0, question);
        targetQuestions.forEach((q, idx) => q.order = idx + 1);

        if (sourceSection !== target) {
            const sourceQuestions = this.state.questions
                .filter(q => (q.sectionId ?? null) === sourceSection && q.id !== id)
                .sort((a, b) => a.order - b.order);
            sourceQuestions.forEach((q, idx) => q.order = idx + 1);
        }

        this.emit();
    }

    setRules(rules = []) {
        this.state.rules = rules.map(rule => ({
            id: rule.id ?? this.newId(),
            sourceQuestionId: rule.sourceQuestionId,
            condition: rule.condition,
            action: rule.action,
        }));
        this.emit();
    }

    addRule(rule) {
        const newRule = {
            id: rule.id ?? this.newId(),
            sourceQuestionId: rule.sourceQuestionId,
            condition: rule.condition,
            action: rule.action,
        };
        this.state.rules.push(newRule);
        this.emit();
        return newRule;
    }

    deleteRule(id) {
        this.state.rules = this.state.rules.filter(r => r.id !== id);
        this.emit();
    }

    toJSON() {
        const sections = this.state.sections
            .slice()
            .sort((a, b) => a.order - b.order)
            .map((sec, index) => ({
                id: sec.id,
                title: sec.title,
                order: index + 1,
                columns: sec.columns,
            }));

        const questions = this.state.questions
            .slice()
            .sort((a, b) => a.order - b.order)
            .map((q, index) => {
                const payload = {
                    id: q.id,
                    sectionId: q.sectionId,
                    type: q.type,
                    prompt: q.prompt,
                    key: q.key,
                    required: q.required,
                    order: index + 1,
                    settings: this.buildSettings(q),
                };

                const visibleIf = typeof q.visibleIf === "string" ? q.visibleIf.trim() : "";
                if (visibleIf) {
                    payload.visibleIf = visibleIf;
                }

                return payload;
            });

        let optionOrder = 1;
        const options = this.state.questions.flatMap(q => q.choices.map(choice => ({
            id: choice.id,
            questionId: q.id,
            value: choice.value,
            label: choice.label,
            order: optionOrder++,
        })));

        const rules = this.state.rules.map(rule => ({
            id: rule.id,
            sourceQuestionId: rule.sourceQuestionId,
            condition: rule.condition,
            action: rule.action,
        }));

        const payload = {
            code: this.state.code,
            title: this.state.title,
            description: this.state.description ?? null,
            sections,
            questions,
            options,
            rules,
        };

        if (this.state.theme) {
            payload.theme = { ...this.state.theme };
        }

        return payload;
    }

    buildSettings(question) {
        const settings = { ...question.settings };
        if (question.validations?.length) settings.validations = [...question.validations];
        if (question.choices?.length) settings.choices = question.choices.map(choice => ({
            id: choice.id,
            value: choice.value,
            label: choice.label,
        }));
        return Object.keys(settings).length ? settings : undefined;
    }

    ensureSectionOrder() {
        this.state.sections
            .sort((a, b) => a.order - b.order)
            .forEach((sec, index) => { sec.order = index + 1; });
    }

    ensureAllQuestionOrder() {
        const grouped = new Map();
        this.state.questions.forEach(question => {
            const key = question.sectionId ?? null;
            if (!grouped.has(key)) grouped.set(key, []);
            grouped.get(key).push(question);
        });
        grouped.forEach(list => {
            list.sort((a, b) => a.order - b.order);
            list.forEach((q, idx) => { q.order = idx + 1; });
        });
    }

    ensureQuestionOrder(sectionId) {
        const list = this.state.questions
            .filter(q => (q.sectionId ?? null) === (sectionId ?? null))
            .sort((a, b) => a.order - b.order);
        list.forEach((q, idx) => { q.order = idx + 1; });
    }

    defaultPrompt(type) {
        switch (type) {
            case "nps": return "How likely are you to recommend us?";
            case "likert": return "Rate your agreement";
            case "single": return "Select one option";
            case "multi": return "Select all that apply";
            case "staff": return "Which staff member helped you?";
            case "phone": return "Phone number";
            case "number": return "Numeric value";
            case "date": return "Pick a date";
            case "boolean": return "Yes or No";
            case "matrix": return "Matrix question";
            case "file": return "Upload a file";
            case "signature": return "Provide your signature";
            case "rating_stars": return "Rate your experience";
            case "image":
            case "video":
            case "static_text":
            case "static_html":
            case "divider":
            case "spacer":
                return "";
            default: return "Your response";
        }
    }

    defaultKey(type, index) {
        if (type === "staff") {
            const base = "staff";
            const exists = this.state.questions.some(q => (q.key || "").toLowerCase() === base);
            if (!exists) return base;
        }
        return this.makeKey(type, index);
    }

    defaultChoices(type) {
        if (type === "nps") {
            return Array.from({ length: 11 }, (_, i) => ({
                id: this.newId(),
                value: String(i),
                label: String(i),
            }));
        }
        if (type === "likert") {
            return ["Strongly disagree", "Disagree", "Neutral", "Agree", "Strongly agree"].map(label => ({
                id: this.newId(),
                value: this.makeKey(label, 1),
                label,
            }));
        }
        if (type === "single" || type === "multi") {
            return [1, 2, 3].map(i => ({
                id: this.newId(),
                value: `option_${i}`,
                label: `Option ${i}`,
            }));
        }
        if (type === "boolean") {
            return [
                { id: this.newId(), value: "yes", label: "Yes" },
                { id: this.newId(), value: "no", label: "No" },
            ];
        }
        return [];
    }

    defaultSettings(type) {
        switch (type) {
            case "static_text":
                return { text: "Static text", alignment: "left" };
            case "static_html":
                return { html: "<p>Static HTML</p>", scopedCss: "" };
            case "image":
                return { url: "", alt: "", caption: "", width: "", height: "" };
            case "video":
                return { url: "", poster: "", autoplay: false, loop: false, caption: "" };
            case "divider":
                return { style: "solid", color: "", thickness: 1 };
            case "spacer":
                return { size: "md", customHeight: null };
            case "matrix":
                return {
                    rows: [
                        { id: this.newId(), label: "Row 1" },
                        { id: this.newId(), label: "Row 2" },
                    ],
                    columns: [
                        { id: this.newId(), label: "Column 1", type: "text" },
                        { id: this.newId(), label: "Column 2", type: "text" },
                    ],
                    cellType: "text",
                };
            case "file":
                return { allowedTypes: ["image/*", "application/pdf"], maxFileSize: null, maxFiles: 1 };
            case "signature":
                return { backgroundColor: "#ffffff", penColor: "#000000", showGuideline: true };
            case "rating_stars":
                return { stars: 5, icon: "star", showLabels: false };
            case "staff":
                return {
                    source: "staff",
                    sourceUrl: "",
                    labelField: "name",
                    valueField: "id",
                    imageField: "photoUrl",
                    includeInactive: false,
                    columns: 2,
                };
            default:
                return {};
        }
    }

    mergeSettings(defaults = {}, overrides = {}) {
        const base = this.cloneValue(defaults);
        const source = overrides ?? {};
        for (const key of Object.keys(source)) {
            base[key] = this.cloneValue(source[key]);
        }
        return base;
    }

    cloneValue(value) {
        if (Array.isArray(value)) {
            return value.map(item => this.cloneValue(item));
        }
        if (value && typeof value === "object") {
            const clone = {};
            for (const key of Object.keys(value)) {
                clone[key] = this.cloneValue(value[key]);
            }
            return clone;
        }
        return value;
    }

    isStaticType(type) {
        return typeof type === "string" && (type.startsWith("static_") || type === "divider" || type === "spacer" || type === "image" || type === "video");
    }

    makeKey(prefix, index) {
        const base = (prefix || "").toString().toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "");
        return `${base || "question"}_${index}`;
    }

    newId() {
        if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
            return crypto.randomUUID();
        }
        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, c => {
            const r = Math.random() * 16 | 0;
            const v = c === "x" ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    snapshot() {
        return JSON.parse(JSON.stringify(this.state));
    }

    emit() {
        const snapshot = this.snapshot();
        this.listeners.forEach(listener => listener(snapshot));
    }
}
