(function () {
    const app = document.getElementById('app');
    if (!app) {
        console.warn('Survey app container not found.');
        return;
    }
    if (typeof window.SurveyRenderer === 'undefined') {
        console.error('SurveyRenderer is not available.');
        app.classList.remove('loading');
        app.textContent = 'Survey renderer is not available.';
        return;
    }

    const apiFetch = typeof window.appApiFetch === 'function'
        ? window.appApiFetch.bind(window)
        : (resource, init) => window.fetch(resource, init);

    let sessionStarted = Date.now();

    function resolveSurveyCode() {
        const explicit = app.dataset.surveyCode || app.dataset.code;
        if (explicit) return explicit;
        const params = new URLSearchParams(window.location.search);
        return params.get('code') || params.get('survey') || '';
    }

    const surveyCode = resolveSurveyCode();
    if (!surveyCode) {
        app.classList.remove('loading');
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
        const response = await apiFetch(`/api/surveys/${encodeURIComponent(surveyCode)}/definition`, { headers });
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

    const staticQuestionTypes = new Set([
        'static_text',
        'static_html',
        'image',
        'video',
        'divider',
        'spacer',
        'message',
        'note',
        'info'
    ]);

    function isStaticType(type) {
        if (typeof type !== 'string') return false;
        return staticQuestionTypes.has(type.trim().toLowerCase());
    }

    function createQuestionElement(question, state) {
        const container = document.createElement('div');
        container.className = 'survey-question';
        container.dataset.questionKey = question.key;
        if (question.visibleIf) {
            container.dataset.visibleIf = question.visibleIf;
        }

        const result = { question, container, error: null };
        const isStatic = isStaticType(question.type);
        if (isStatic) {
            container.classList.add('survey-question-static');
        }
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

    function buildSubmitPayload(questions, state) {
        const payload = {};
        questions.forEach(question => {
            if (isStaticType(question.type)) {
                return;
            }

            const value = state.answers[question.key];
            if (value === undefined || value === null) {
                payload[question.key] = null;
                return;
            }

            if (Array.isArray(value)) {
                if (value.length === 0) {
                    payload[question.key] = null;
                } else if (String(question.type).trim().toLowerCase() === 'multi') {
                    payload[question.key] = value.map(item => String(item)).join(',');
                } else {
                    const serialised = JSON.stringify(value);
                    payload[question.key] = serialised === '[]' ? null : serialised;
                }
                return;
            }

            if (typeof value === 'object') {
                const serialised = JSON.stringify(value);
                payload[question.key] = serialised === '{}' ? null : serialised;
                return;
            }

            const str = String(value);
            payload[question.key] = str.trim() === '' ? null : str;
        });

        payload.__startedUtc = new Date(sessionStarted).toISOString();
        payload.__durationSeconds = String(Math.max(0, Math.round((Date.now() - sessionStarted) / 1000)));

        return payload;
    }

    function renderSurvey(definition) {
        sessionStarted = Date.now();
        app.classList.remove('loading');
        app.innerHTML = '';
        const state = createState();
        const parsedQuestions = (definition.questions ?? [])
            .slice()
            .sort((a, b) => a.order - b.order)
            .map(parseQuestion);
        const parsedById = new Map(parsedQuestions.map(q => [q.id, q]));
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
            } else if (!isStaticType(question.type)) {
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

        function appendSection(sectionMeta, questions, fallbackTitle) {
            if (!questions || questions.length === 0) {
                return;
            }
            const sectionContainer = document.createElement('section');
            sectionContainer.className = 'survey-section';
            if (sectionMeta && sectionMeta.id) {
                sectionContainer.dataset.sectionId = sectionMeta.id;
            }
            const headingText = (sectionMeta && sectionMeta.title) || fallbackTitle || null;
            if (headingText) {
                const heading = document.createElement('h2');
                heading.textContent = headingText;
                sectionContainer.appendChild(heading);
            }
            const localRefs = [];
            questions.forEach(question => {
                const parsed = parsedById.get(question.id) ?? parseQuestion(question);
                const ref = createQuestionElement(parsed, state);
                sectionContainer.appendChild(ref.container);
                questionRefs.push(ref);
                localRefs.push(ref);
            });
            if (localRefs.length === 0) {
                sectionContainer.hidden = true;
            }
            form.appendChild(sectionContainer);
            sectionRefs.push({ container: sectionContainer, questions: localRefs });
        }

        sections.forEach(section => {
            const questions = grouped.get(section.id) ?? [];
            appendSection(section, questions);
        });

        const ungroupedQuestions = grouped.get(ungroupedKey) ?? [];
        if (ungroupedQuestions.length > 0 || sectionRefs.length === 0) {
            appendSection(null, ungroupedQuestions, sectionRefs.length === 0 ? definition.title : null);
        }

        const statusMessage = document.createElement('div');
        statusMessage.className = 'survey-status';
        statusMessage.setAttribute('role', 'status');
        statusMessage.setAttribute('aria-live', 'polite');

        const nav = document.createElement('div');
        nav.className = 'survey-nav';

        const progressContainer = document.createElement('div');
        progressContainer.className = 'survey-progress';
        const progressLabel = document.createElement('span');
        const progressBar = document.createElement('div');
        progressBar.className = 'survey-progress-bar';
        const progressFill = document.createElement('span');
        progressFill.style.width = '0%';
        progressBar.appendChild(progressFill);
        progressContainer.appendChild(progressLabel);
        progressContainer.appendChild(progressBar);

        const navButtons = document.createElement('div');
        navButtons.className = 'survey-nav-buttons';

        const backButton = document.createElement('button');
        backButton.type = 'button';
        backButton.className = 'secondary';
        backButton.textContent = 'Back';

        const nextButton = document.createElement('button');
        nextButton.type = 'button';
        nextButton.className = 'primary';
        nextButton.textContent = 'Next';

        const submitButton = document.createElement('button');
        submitButton.type = 'submit';
        submitButton.className = 'primary';
        submitButton.textContent = 'Submit';

        navButtons.appendChild(backButton);
        navButtons.appendChild(nextButton);
        navButtons.appendChild(submitButton);
        nav.appendChild(progressContainer);
        nav.appendChild(navButtons);

        form.appendChild(nav);
        form.appendChild(statusMessage);

        let currentStep = 0;

        function visibleSectionEntries() {
            return sectionRefs
                .map((ref, index) => ({ ref, index }))
                .filter(entry => !entry.ref.container.hidden);
        }

        function showStep(requestedIndex) {
            const visibleEntries = visibleSectionEntries();
            if (visibleEntries.length === 0) {
                sectionRefs.forEach(section => {
                    section.container.classList.remove('active');
                    section.container.style.display = 'none';
                });
                nav.style.display = 'none';
                progressLabel.textContent = '';
                progressFill.style.width = '0%';
                currentStep = 0;
                return;
            }

            nav.style.display = '';
            const targetIndex = Math.min(Math.max(requestedIndex, 0), visibleEntries.length - 1);

            visibleEntries.forEach((entry, idx) => {
                const isActive = idx === targetIndex;
                entry.ref.container.classList.toggle('active', isActive);
                entry.ref.container.style.display = isActive ? '' : 'none';
            });

            sectionRefs.forEach(section => {
                if (section.container.hidden) {
                    section.container.classList.remove('active');
                    section.container.style.display = 'none';
                }
            });

            currentStep = targetIndex;

            const isFirst = currentStep === 0;
            const isLast = currentStep === visibleEntries.length - 1;

            backButton.disabled = isFirst;
            backButton.hidden = visibleEntries.length <= 1;
            nextButton.hidden = isLast;
            submitButton.hidden = !isLast;

            progressLabel.textContent = `Step ${currentStep + 1} of ${visibleEntries.length}`;
            progressFill.style.width = `${((currentStep + 1) / visibleEntries.length) * 100}%`;
            progressContainer.style.visibility = visibleEntries.length <= 1 ? 'hidden' : 'visible';
        }

        backButton.addEventListener('click', () => {
            showStep(currentStep - 1);
        });

        nextButton.addEventListener('click', () => {
            showStep(currentStep + 1);
        });

        let attemptedSubmit = false;

        state.onChange(() => {
            updateVisibility(questionRefs, state, sectionRefs);
            if (attemptedSubmit) {
                const validation = SurveyRenderer.validate(parsedQuestions, state);
                syncErrors(questionRefs, validation.errors);
            }
            showStep(currentStep);
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
                const answers = buildSubmitPayload(parsedQuestions, state);
                const response = await apiFetch(`/api/surveys/${encodeURIComponent(surveyCode)}/submit`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ answers })
                });
                if (!response.ok) {
                    throw new Error('Submit failed');
                }
                statusMessage.textContent = 'Thank you for your response!';
                form.reset();
                parsedQuestions.forEach(question => {
                    if (isStaticType(question.type)) {
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
                sessionStarted = Date.now();
                showStep(0);
            } catch (error) {
                console.error(error);
                statusMessage.textContent = 'There was a problem submitting your response.';
            } finally {
                submitButton.disabled = false;
            }
        });

        app.appendChild(form);
        updateVisibility(questionRefs, state, sectionRefs);
        showStep(0);
    }

    loadDefinition()
        .then(renderSurvey)
        .catch(error => {
            console.error(error);
            app.classList.remove('loading');
            app.textContent = 'Unable to load the survey.';
        });
})();
