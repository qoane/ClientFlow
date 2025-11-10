(function () {
    const app = document.getElementById('app');
    if (!app) {
        console.warn('Survey app container not found.');
        return;
    }
    if (typeof window.SurveyRenderer === 'undefined') {
        console.error('SurveyRenderer is not available.');
        app.textContent = 'Survey renderer is not available.';
        return;
    }

    function resolveSurveyCode() {
        const explicit = app.dataset.surveyCode || app.dataset.code;
        if (explicit) return explicit;
        const params = new URLSearchParams(window.location.search);
        return params.get('code') || params.get('survey') || '';
    }

    const surveyCode = resolveSurveyCode();
    if (!surveyCode) {
        app.textContent = 'Survey code is missing.';
        return;
    }

    let etag = null;
    let cachedDefinition = null;

    async function loadDefinition() {
        const headers = {};
        if (etag) {
            headers['If-None-Match'] = etag;
        }
        const response = await fetch(`/api/surveys/${encodeURIComponent(surveyCode)}/definition`, { headers });
        if (response.status === 304 && cachedDefinition) {
            return cachedDefinition;
        }
        if (!response.ok) {
            throw new Error('Unable to load survey definition');
        }
        etag = response.headers.get('ETag');
        cachedDefinition = await response.json();
        return cachedDefinition;
    }

    function createState() {
        const listeners = [];
        const state = {
            answers: {},
            errors: {},
            setAnswer(key, value) {
                this.answers[key] = value;
                listeners.forEach(listener => {
                    try {
                        listener(key, value);
                    } catch (error) {
                        console.error('State change listener failed', error);
                    }
                });
            },
            onChange(listener) {
                listeners.push(listener);
            }
        };
        return state;
    }

    function groupQuestionsBySection(definition) {
        const sections = definition.sections?.slice().sort((a, b) => a.order - b.order) ?? [];
        const sectionLookup = new Map();
        sections.forEach(section => {
            sectionLookup.set(section.id, section);
        });
        const ungroupedKey = Symbol('default');
        const grouped = new Map();
        sections.forEach(section => grouped.set(section.id, []));
        grouped.set(ungroupedKey, []);

        const orderedQuestions = definition.questions?.slice().sort((a, b) => a.order - b.order) ?? [];
        orderedQuestions.forEach(question => {
            const key = question.sectionId && sectionLookup.has(question.sectionId)
                ? question.sectionId
                : ungroupedKey;
            grouped.get(key).push(question);
        });

        return { sections, grouped, ungroupedKey };
    }

    function parseQuestion(question) {
        let settings = {};
        if (typeof question.settingsJson === 'string' && question.settingsJson.trim().length > 0) {
            try {
                settings = JSON.parse(question.settingsJson);
            } catch (error) {
                console.warn('Failed to parse question settings', error);
            }
        }
        return {
            ...question,
            settings,
            options: Array.isArray(question.options) ? question.options : []
        };
    }

    function createQuestionElement(question, state) {
        const container = document.createElement('div');
        container.className = 'survey-question';
        container.dataset.questionKey = question.key;
        if (question.visibleIf) {
            container.dataset.visibleIf = question.visibleIf;
        }

        const result = { question, container, error: null };
        const isStatic = ['static_text', 'static_html', 'image', 'video', 'divider', 'spacer'].includes(question.type);
        if (!isStatic) {
            const prompt = document.createElement('label');
            prompt.className = 'survey-prompt';
            prompt.textContent = question.prompt ?? '';
            container.appendChild(prompt);
        }

        const field = SurveyRenderer.renderQuestion(question, state);
        if (field) {
            container.appendChild(field);
        }

        if (!isStatic) {
            const error = document.createElement('div');
            error.className = 'survey-error';
            error.setAttribute('role', 'alert');
            error.setAttribute('aria-live', 'polite');
            container.appendChild(error);
            result.error = error;
        }

        return result;
    }

    function updateVisibility(questionRefs, state, sectionRefs) {
        questionRefs.forEach(ref => {
            const visible = SurveyRenderer.evaluateVisible(ref.question.visibleIf, state);
            ref.container.hidden = !visible;
        });
        if (sectionRefs) {
            sectionRefs.forEach(section => {
                const anyVisible = section.questions.some(ref => !ref.container.hidden);
                section.container.hidden = !anyVisible;
            });
        }
    }

    function syncErrors(questionRefs, errors) {
        questionRefs.forEach(ref => {
            if (!ref.error) return;
            const messageList = errors[ref.question.key];
            const hasErrors = Array.isArray(messageList) && messageList.length > 0;
            ref.error.textContent = hasErrors ? messageList.join(' ') : '';
            ref.container.classList.toggle('has-error', hasErrors);
        });
    }

    function renderSurvey(definition) {
        app.innerHTML = '';
        const state = createState();
        const parsedQuestions = (definition.questions ?? [])
            .slice()
            .sort((a, b) => a.order - b.order)
            .map(parseQuestion);
        parsedQuestions.forEach(question => {
            if (question.type === 'multi') {
                state.answers[question.key] = Array.isArray(question.settings?.defaultValue)
                    ? [...question.settings.defaultValue]
                    : [];
            } else if (question.type === 'file') {
                state.answers[question.key] = [];
            } else if (question.type === 'matrix') {
                if (question.settings && question.settings.defaultValue && typeof question.settings.defaultValue === 'object') {
                    state.answers[question.key] = JSON.parse(JSON.stringify(question.settings.defaultValue));
                } else {
                    state.answers[question.key] = {};
                }
            } else if (!['static_text', 'static_html', 'image', 'video', 'divider', 'spacer'].includes(question.type)) {
                if (question.settings && Object.prototype.hasOwnProperty.call(question.settings, 'defaultValue')) {
                    state.answers[question.key] = question.settings.defaultValue;
                } else {
                    state.answers[question.key] = null;
                }
            }
        });

        const { sections, grouped, ungroupedKey } = groupQuestionsBySection(definition);
        const form = document.createElement('form');
        form.className = 'survey-form';
        const questionRefs = [];
        const sectionRefs = [];

        sections.forEach(section => {
            const sectionContainer = document.createElement('section');
            sectionContainer.className = 'survey-section';
            sectionContainer.dataset.sectionId = section.id;
            if (section.title) {
                const heading = document.createElement('h2');
                heading.textContent = section.title;
                sectionContainer.appendChild(heading);
            }
            const questions = grouped.get(section.id) ?? [];
            const localRefs = [];
            questions.forEach(question => {
                const parsed = parsedQuestions.find(q => q.id === question.id) ?? parseQuestion(question);
                const ref = createQuestionElement(parsed, state);
                sectionContainer.appendChild(ref.container);
                questionRefs.push(ref);
                localRefs.push(ref);
            });
            form.appendChild(sectionContainer);
            sectionRefs.push({ container: sectionContainer, questions: localRefs });
        });

        const ungroupedQuestions = grouped.get(ungroupedKey) ?? [];
        ungroupedQuestions.forEach(question => {
            const parsed = parsedQuestions.find(q => q.id === question.id) ?? parseQuestion(question);
            const ref = createQuestionElement(parsed, state);
            form.appendChild(ref.container);
            questionRefs.push(ref);
        });

        const statusMessage = document.createElement('div');
        statusMessage.className = 'survey-status';
        statusMessage.setAttribute('role', 'status');
        statusMessage.setAttribute('aria-live', 'polite');

        const submitButton = document.createElement('button');
        submitButton.type = 'submit';
        submitButton.textContent = 'Submit';
        form.appendChild(submitButton);
        form.appendChild(statusMessage);

        let attemptedSubmit = false;

        state.onChange(() => {
            updateVisibility(questionRefs, state, sectionRefs);
            if (attemptedSubmit) {
                const validation = SurveyRenderer.validate(parsedQuestions, state);
                syncErrors(questionRefs, validation.errors);
            }
        });

        form.addEventListener('submit', async event => {
            event.preventDefault();
            attemptedSubmit = true;
            updateVisibility(questionRefs, state, sectionRefs);
            const validation = SurveyRenderer.validate(parsedQuestions, state);
            syncErrors(questionRefs, validation.errors);
            if (!validation.valid) {
                statusMessage.textContent = 'Please review the highlighted responses.';
                return;
            }
            statusMessage.textContent = 'Submitting…';
            submitButton.disabled = true;
            try {
                const response = await fetch(`/api/surveys/${encodeURIComponent(surveyCode)}/responses`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ data: state.answers })
                });
                if (!response.ok) {
                    throw new Error('Submit failed');
                }
                statusMessage.textContent = 'Thank you for your response!';
                form.reset();
                parsedQuestions.forEach(question => {
                    if (['static_text', 'static_html', 'image', 'video', 'divider', 'spacer'].includes(question.type)) {
                        return;
                    }
                    if (question.type === 'multi') {
                        const defaults = Array.isArray(question.settings?.defaultValue)
                            ? [...question.settings.defaultValue]
                            : [];
                        state.setAnswer(question.key, defaults);
                    } else if (question.type === 'file') {
                        state.setAnswer(question.key, []);
                    } else if (question.type === 'matrix') {
                        const baseValue = question.settings && question.settings.defaultValue && typeof question.settings.defaultValue === 'object'
                            ? JSON.parse(JSON.stringify(question.settings.defaultValue))
                            : {};
                        state.setAnswer(question.key, baseValue);
                    } else {
                        const hasDefault = question.settings && Object.prototype.hasOwnProperty.call(question.settings, 'defaultValue');
                        state.setAnswer(question.key, hasDefault ? question.settings.defaultValue : null);
                    }
                });
                attemptedSubmit = false;
                state.errors = {};
                syncErrors(questionRefs, {});
            } catch (error) {
                console.error(error);
                statusMessage.textContent = 'There was a problem submitting your response.';
            } finally {
                submitButton.disabled = false;
            }
        });

        app.appendChild(form);
        updateVisibility(questionRefs, state, sectionRefs);
    }

    loadDefinition()
        .then(renderSurvey)
        .catch(error => {
            console.error(error);
            app.textContent = 'Unable to load the survey.';
        });
})();
