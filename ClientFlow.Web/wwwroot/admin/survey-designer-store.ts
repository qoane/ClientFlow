export interface SurveyDesignerChoice {
    id: string;
    value: string;
    label: string;
}

export interface SurveyDesignerQuestion {
    id: string;
    sectionId: string | null;
    type: string;
    prompt: string;
    key: string;
    required: boolean;
    order: number;
    settings: Record<string, unknown>;
    choices: SurveyDesignerChoice[];
    validations: string[];
    visibility: string;
}

export interface SurveyDesignerSection {
    id: string;
    title: string;
    order: number;
    columns: number;
}

export interface SurveyDesignerRule {
    id: string;
    sourceQuestionId: string;
    condition: string;
    action: string;
}

export interface SurveyDesignerTheme {
    accent?: string;
    panel?: string;
}

export interface SurveyDesignerState {
    code: string;
    title: string;
    description?: string;
    sections: SurveyDesignerSection[];
    questions: SurveyDesignerQuestion[];
    rules: SurveyDesignerRule[];
    theme?: SurveyDesignerTheme;
}

export interface SurveyDesignerQuestionInput extends Partial<Omit<SurveyDesignerQuestion, "id" | "order">> {
    id?: string;
    order?: number;
}

export class SurveyDesignerStore {
    private state: SurveyDesignerState;
    private listeners: Set<(state: SurveyDesignerState) => void> = new Set();

    constructor(initial?: Partial<SurveyDesignerState>) {
        this.state = {
            code: initial?.code ?? "",
            title: initial?.title ?? "",
            description: initial?.description,
            sections: [],
            questions: [],
            rules: [],
            theme: initial?.theme ? { ...initial.theme } : undefined,
        };

        if (initial?.sections || initial?.questions || initial?.rules) {
            this.load(initial);
        }
    }

    subscribe(listener: (state: SurveyDesignerState) => void): () => void {
        this.listeners.add(listener);
        listener(this.snapshot());
        return () => this.listeners.delete(listener);
    }

    load(data: Partial<SurveyDesignerState>): void {
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
                settings: { ...(q.settings ?? {}) },
                choices: (q.choices ?? []).map((choice, ci) => ({
                    id: choice.id ?? this.newId(),
                    value: choice.value ?? choice.label ?? `choice_${ci + 1}`,
                    label: choice.label ?? choice.value ?? `Choice ${ci + 1}`,
                })),
                validations: Array.isArray(q.validations) ? [...q.validations] : [],
                visibility: q.visibility ?? "",
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

    setMeta(meta: { code?: string; title?: string; description?: string }): void {
        if (meta.code !== undefined) this.state.code = meta.code;
        if (meta.title !== undefined) this.state.title = meta.title;
        if (meta.description !== undefined) this.state.description = meta.description;
        this.emit();
    }

    setTheme(theme: SurveyDesignerTheme | undefined): void {
        this.state.theme = theme ? { ...theme } : undefined;
        this.emit();
    }

    getState(): SurveyDesignerState {
        return this.snapshot();
    }

    addSection(title = "New Section", columns = 1): SurveyDesignerSection {
        const section: SurveyDesignerSection = {
            id: this.newId(),
            title: title.trim() || "New Section",
            columns: columns <= 0 ? 1 : Math.min(columns, 2),
            order: this.state.sections.length + 1,
        };
        this.state.sections.push(section);
        this.emit();
        return section;
    }

    updateSection(id: string, patch: Partial<Omit<SurveyDesignerSection, "id">>): void {
        const section = this.state.sections.find(sec => sec.id === id);
        if (!section) return;
        if (patch.title !== undefined) section.title = patch.title.trim() || section.title;
        if (patch.columns !== undefined) section.columns = patch.columns <= 0 ? 1 : Math.min(patch.columns, 2);
        this.emit();
    }

    moveSection(id: string, newIndex: number): void {
        const sections = this.state.sections;
        const idx = sections.findIndex(sec => sec.id === id);
        if (idx === -1) return;
        const [section] = sections.splice(idx, 1);
        newIndex = Math.max(0, Math.min(newIndex, sections.length));
        sections.splice(newIndex, 0, section);
        this.ensureSectionOrder();
        this.emit();
    }

    deleteSection(id: string): void {
        this.state.sections = this.state.sections.filter(sec => sec.id !== id);
        this.state.questions = this.state.questions.filter(q => q.sectionId !== id);
        this.ensureSectionOrder();
        this.ensureAllQuestionOrder();
        this.emit();
    }

    addQuestion(sectionId: string | null, type: string, input?: SurveyDesignerQuestionInput): SurveyDesignerQuestion {
        const question: SurveyDesignerQuestion = {
            id: input?.id ?? this.newId(),
            sectionId,
            type,
            prompt: input?.prompt ?? this.defaultPrompt(type),
            key: input?.key ?? this.makeKey(type, this.state.questions.length + 1),
            required: input?.required ?? false,
            order: 0,
            settings: { ...(input?.settings ?? {}) },
            choices: input?.choices ? input.choices.map(choice => ({
                id: choice.id ?? this.newId(),
                value: choice.value ?? choice.label ?? this.makeKey("choice", 1),
                label: choice.label ?? choice.value ?? "Choice",
            })) : this.defaultChoices(type),
            validations: input?.validations ? [...input.validations] : [],
            visibility: input?.visibility ?? "",
        };
        this.state.questions.push(question);
        this.ensureQuestionOrder(sectionId);
        this.emit();
        return question;
    }

    updateQuestion(id: string, patch: Partial<Omit<SurveyDesignerQuestion, "id" | "order">> & { order?: number }): void {
        const question = this.state.questions.find(q => q.id === id);
        if (!question) return;
        if (patch.sectionId !== undefined) question.sectionId = patch.sectionId;
        if (patch.type !== undefined) question.type = patch.type;
        if (patch.prompt !== undefined) question.prompt = patch.prompt;
        if (patch.key !== undefined) question.key = patch.key;
        if (patch.required !== undefined) question.required = patch.required;
        if (patch.settings !== undefined) question.settings = { ...patch.settings };
        if (patch.choices !== undefined) {
            question.choices = patch.choices.map(choice => ({
                id: choice.id ?? this.newId(),
                value: choice.value ?? choice.label ?? this.makeKey("choice", 1),
                label: choice.label ?? choice.value ?? "Choice",
            }));
        }
        if (patch.validations !== undefined) question.validations = [...patch.validations];
        if (patch.visibility !== undefined) question.visibility = patch.visibility;
        if (patch.order !== undefined) question.order = patch.order;
        this.ensureAllQuestionOrder();
        this.emit();
    }

    deleteQuestion(id: string): void {
        const question = this.state.questions.find(q => q.id === id);
        if (!question) return;
        const sectionId = question.sectionId;
        this.state.questions = this.state.questions.filter(q => q.id !== id);
        this.ensureQuestionOrder(sectionId);
        this.emit();
    }

    moveQuestion(id: string, targetSectionId: string | null, index: number): void {
        const question = this.state.questions.find(q => q.id === id);
        if (!question) return;
        const sourceSection = question.sectionId;
        question.sectionId = targetSectionId;

        const targetQuestions = this.state.questions
            .filter(q => q.sectionId === targetSectionId && q.id !== id)
            .sort((a, b) => a.order - b.order);
        index = Math.max(0, Math.min(index, targetQuestions.length));
        targetQuestions.splice(index, 0, question);
        targetQuestions.forEach((q, idx) => q.order = idx + 1);

        if (sourceSection !== targetSectionId) {
            const sourceQuestions = this.state.questions
                .filter(q => q.sectionId === sourceSection && q.id !== id)
                .sort((a, b) => a.order - b.order);
            sourceQuestions.forEach((q, idx) => q.order = idx + 1);
        }

        this.emit();
    }

    setRules(rules: SurveyDesignerRule[]): void {
        this.state.rules = rules.map(rule => ({
            id: rule.id ?? this.newId(),
            sourceQuestionId: rule.sourceQuestionId,
            condition: rule.condition,
            action: rule.action,
        }));
        this.emit();
    }

    addRule(rule: Partial<SurveyDesignerRule> & { sourceQuestionId: string; condition: string; action: string }): SurveyDesignerRule {
        const newRule: SurveyDesignerRule = {
            id: rule.id ?? this.newId(),
            sourceQuestionId: rule.sourceQuestionId,
            condition: rule.condition,
            action: rule.action,
        };
        this.state.rules.push(newRule);
        this.emit();
        return newRule;
    }

    deleteRule(id: string): void {
        this.state.rules = this.state.rules.filter(r => r.id !== id);
        this.emit();
    }

    toJSON(): Record<string, unknown> {
        const sections = this.state.sections
            .slice()
            .sort((a, b) => a.order - b.order)
            .map((sec, index) => ({
                id: sec.id,
                title: sec.title,
                order: index + 1,
                columns: sec.columns,
            }));

        const questionMap = new Map<string, SurveyDesignerQuestion>();
        this.state.questions.forEach(q => questionMap.set(q.id, q));

        const questions = this.state.questions
            .slice()
            .sort((a, b) => a.order - b.order)
            .map((q, index) => ({
                id: q.id,
                sectionId: q.sectionId,
                type: q.type,
                prompt: q.prompt,
                key: q.key,
                required: q.required,
                order: index + 1,
                settings: this.buildSettings(q),
            }));

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

        const payload: Record<string, unknown> = {
            code: this.state.code,
            title: this.state.title,
            description: this.state.description ?? null,
            sections,
            questions,
            options,
            rules,
        };

        if (this.state.theme) {
            payload["theme"] = { ...this.state.theme };
        }

        return payload;
    }

    private buildSettings(question: SurveyDesignerQuestion): Record<string, unknown> | undefined {
        const settings: Record<string, unknown> = { ...question.settings };
        if (question.validations.length) settings["validations"] = [...question.validations];
        if (question.visibility) settings["visibility"] = question.visibility;
        if (question.choices.length) settings["choices"] = question.choices.map(choice => ({
            id: choice.id,
            value: choice.value,
            label: choice.label,
        }));
        return Object.keys(settings).length ? settings : undefined;
    }

    private ensureSectionOrder(): void {
        this.state.sections
            .sort((a, b) => a.order - b.order)
            .forEach((sec, index) => sec.order = index + 1);
    }

    private ensureAllQuestionOrder(): void {
        const grouped = new Map<string | null, SurveyDesignerQuestion[]>();
        this.state.questions.forEach(question => {
            const key = question.sectionId ?? null;
            if (!grouped.has(key)) grouped.set(key, []);
            grouped.get(key)!.push(question);
        });
        grouped.forEach(list => {
            list.sort((a, b) => a.order - b.order);
            list.forEach((q, idx) => q.order = idx + 1);
        });
    }

    private ensureQuestionOrder(sectionId: string | null): void {
        const list = this.state.questions
            .filter(q => q.sectionId === sectionId)
            .sort((a, b) => a.order - b.order);
        list.forEach((q, idx) => q.order = idx + 1);
    }

    private defaultPrompt(type: string): string {
        switch (type) {
            case "nps":
                return "How likely are you to recommend us?";
            case "likert":
                return "Rate your agreement";
            case "single":
                return "Select one option";
            case "multi":
                return "Select all that apply";
            case "phone":
                return "Phone number";
            case "number":
                return "Numeric value";
            case "date":
                return "Pick a date";
            case "boolean":
                return "Yes or No";
            default:
                return "Your response";
        }
    }

    private defaultChoices(type: string): SurveyDesignerChoice[] {
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

    private makeKey(prefix: string, index: number): string {
        const base = prefix.toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "");
        return `${base || "question"}_${index}`;
    }

    private newId(): string {
        if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
            return crypto.randomUUID();
        }
        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, c => {
            const r = Math.random() * 16 | 0;
            const v = c === "x" ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    private snapshot(): SurveyDesignerState {
        return JSON.parse(JSON.stringify(this.state));
    }

    private emit(): void {
        const snapshot = this.snapshot();
        this.listeners.forEach(listener => listener(snapshot));
    }
}
