(function (global) {
    const renderers = Object.create(null);

    function coerceBoolean(value) {
        if (typeof value === 'boolean') return value;
        if (typeof value === 'string') {
            const normalized = value.trim().toLowerCase();
            if (normalized === 'true' || normalized === 'yes' || normalized === '1') return true;
            if (normalized === 'false' || normalized === 'no' || normalized === '0') return false;
        }
        return !!value;
    }

    function getCurrentAnswer(q, state, fallback) {
        if (state && state.answers && Object.prototype.hasOwnProperty.call(state.answers, q.key)) {
            return state.answers[q.key];
        }
        if (fallback !== undefined) return fallback;
        if (q.settings && Object.prototype.hasOwnProperty.call(q.settings, 'defaultValue')) {
            return q.settings.defaultValue;
        }
        return null;
    }

    function ensureArray(value) {
        if (Array.isArray(value)) return value;
        if (value === null || value === undefined) return [];
        return [value];
    }

    function createInputRenderer(inputType) {
        return (q, state) => {
            const wrapper = document.createElement('div');
            const input = document.createElement('input');
            input.type = inputType;
            if (inputType === 'tel') {
                input.inputMode = 'tel';
                input.autocomplete = 'tel';
            }
            if (q.settings && typeof q.settings.placeholder === 'string') {
                input.placeholder = q.settings.placeholder;
            }
            const currentValue = getCurrentAnswer(q, state, '');
            if (currentValue !== null && currentValue !== undefined) {
                input.value = String(currentValue);
            }
            input.defaultValue = input.value;
            input.addEventListener('input', () => {
                state.setAnswer(q.key, input.value);
            });
            wrapper.appendChild(input);
            return wrapper;
        };
    }

    function createTextareaRenderer() {
        return (q, state) => {
            const textarea = document.createElement('textarea');
            if (q.settings && typeof q.settings.placeholder === 'string') {
                textarea.placeholder = q.settings.placeholder;
            }
            if (q.settings && typeof q.settings.rows === 'number' && q.settings.rows > 0) {
                textarea.rows = q.settings.rows;
            }
            const currentValue = getCurrentAnswer(q, state, '');
            if (currentValue !== null && currentValue !== undefined) {
                textarea.value = String(currentValue);
            }
            textarea.defaultValue = textarea.value;
            textarea.addEventListener('input', () => {
                state.setAnswer(q.key, textarea.value);
            });
            return textarea;
        };
    }

    function createChoiceRenderer(kind) {
        return (q, state) => {
            const list = document.createElement('div');
            list.className = 'survey-options';
            const current = ensureArray(getCurrentAnswer(q, state, kind === 'multi' ? [] : null));
            const name = `q_${q.key}`;
            const options = Array.isArray(q.options) && q.options.length > 0
                ? q.options
                : (q.settings && Array.isArray(q.settings.choices) ? q.settings.choices : []);
            options.forEach(option => {
                const id = `${name}_${option.value}`;
                const label = document.createElement('label');
                label.setAttribute('for', id);
                label.className = 'survey-option';

                const input = document.createElement('input');
                input.id = id;
                input.name = name;
                input.type = kind === 'multi' ? 'checkbox' : 'radio';
                input.value = option.value;
                if (kind === 'multi') {
                    input.checked = current.includes(option.value);
                } else {
                    input.checked = current.length > 0 ? current[0] === option.value : false;
                }
                input.defaultChecked = input.checked;

                input.addEventListener('change', () => {
                    if (kind === 'multi') {
                        const next = new Set(ensureArray(state.answers[q.key] ?? []));
                        if (input.checked) {
                            next.add(option.value);
                        } else {
                            next.delete(option.value);
                        }
                        state.setAnswer(q.key, Array.from(next));
                    } else {
                        if (input.checked) {
                            state.setAnswer(q.key, option.value);
                        }
                    }
                });

                const span = document.createElement('span');
                span.textContent = option.label;

                label.appendChild(input);
                label.appendChild(span);
                list.appendChild(label);
            });
            return list;
        };
    }

    function yesNoRenderer(q, state) {
        const yesLabel = q.settings && typeof q.settings.yesLabel === 'string' ? q.settings.yesLabel : 'Yes';
        const noLabel = q.settings && typeof q.settings.noLabel === 'string' ? q.settings.noLabel : 'No';
        const options = [
            { value: 'yes', label: yesLabel },
            { value: 'no', label: noLabel }
        ];
        const base = { ...q, options };
        return createChoiceRenderer('single')(base, state);
    }

    function likertRenderer(q, state) {
        const list = document.createElement('div');
        list.className = 'likert-scale';
        const options = Array.isArray(q.options) && q.options.length > 0
            ? q.options
            : [
                { value: 'strongly_disagree', label: 'Strongly disagree' },
                { value: 'disagree', label: 'Disagree' },
                { value: 'neutral', label: 'Neutral' },
                { value: 'agree', label: 'Agree' },
                { value: 'strongly_agree', label: 'Strongly agree' }
            ];
        const current = getCurrentAnswer(q, state, null);
        const name = `q_${q.key}`;
        options.forEach(option => {
            const id = `${name}_${option.value}`;
            const label = document.createElement('label');
            label.setAttribute('for', id);
            label.className = 'survey-option';

            const input = document.createElement('input');
            input.type = 'radio';
            input.name = name;
            input.id = id;
            input.value = option.value;
            input.checked = current === option.value;
            input.defaultChecked = input.checked;
            input.addEventListener('change', () => {
                if (input.checked) {
                    state.setAnswer(q.key, option.value);
                }
            });

            const span = document.createElement('span');
            span.textContent = option.label;
            label.appendChild(input);
            label.appendChild(span);
            list.appendChild(label);
        });
        return list;
    }

    function npsRenderer(q, state) {
        const container = document.createElement('div');
        container.className = 'nps-scale';
        const current = getCurrentAnswer(q, state, null);
        const min = q.settings && typeof q.settings.min === 'number' ? q.settings.min : 0;
        const max = q.settings && typeof q.settings.max === 'number' ? q.settings.max : 10;
        for (let value = min; value <= max; value += 1) {
            const button = document.createElement('button');
            button.type = 'button';
            button.textContent = String(value);
            if (String(current) === String(value)) {
                button.classList.add('selected');
            }
            button.addEventListener('click', () => {
                state.setAnswer(q.key, value);
                Array.from(container.querySelectorAll('button')).forEach(btn => {
                    const isActive = btn === button;
                    btn.classList.toggle('selected', isActive);
                    btn.setAttribute('aria-pressed', isActive ? 'true' : 'false');
                });
            });
            button.setAttribute('aria-pressed', String(current) === String(value) ? 'true' : 'false');
            container.appendChild(button);
        }
        if (typeof state.onChange === 'function') {
            state.onChange((key, value) => {
                if (key !== q.key) return;
                Array.from(container.querySelectorAll('button')).forEach(btn => {
                    const isActive = String(btn.textContent) === String(value);
                    btn.classList.toggle('selected', isActive);
                    btn.setAttribute('aria-pressed', isActive ? 'true' : 'false');
                });
            });
        }
        return container;
    }

    function ratingStarsRenderer(q, state) {
        const wrapper = document.createElement('div');
        wrapper.className = 'rating-stars';
        const maxStars = q.settings && typeof q.settings.stars === 'number' && q.settings.stars > 0 ? q.settings.stars : 5;
        const current = Number(getCurrentAnswer(q, state, 0)) || 0;
        for (let i = 1; i <= maxStars; i += 1) {
            const button = document.createElement('button');
            button.type = 'button';
            button.dataset.value = String(i);
            button.textContent = i <= current ? '★' : '☆';
            button.classList.toggle('selected', i <= current);
            button.setAttribute('aria-pressed', i <= current ? 'true' : 'false');
            button.addEventListener('click', () => {
                const value = Number(button.dataset.value);
                state.setAnswer(q.key, value);
                Array.from(wrapper.querySelectorAll('button')).forEach(btn => {
                    const btnValue = Number(btn.dataset.value);
                    const pressed = btnValue <= value;
                    btn.classList.toggle('selected', pressed);
                    btn.textContent = pressed ? '★' : '☆';
                    btn.setAttribute('aria-pressed', pressed ? 'true' : 'false');
                });
            });
            wrapper.appendChild(button);
        }
        if (typeof state.onChange === 'function') {
            state.onChange((key, value) => {
                if (key !== q.key) return;
                const numericValue = Number(value) || 0;
                Array.from(wrapper.querySelectorAll('button')).forEach(btn => {
                    const btnValue = Number(btn.dataset.value);
                    const pressed = btnValue <= numericValue;
                    btn.classList.toggle('selected', pressed);
                    btn.textContent = pressed ? '★' : '☆';
                    btn.setAttribute('aria-pressed', pressed ? 'true' : 'false');
                });
            });
        }
        return wrapper;
    }

    function matrixRenderer(q, state) {
        const table = document.createElement('table');
        table.className = 'matrix-table';
        const settings = q.settings || {};
        const rows = Array.isArray(settings.rows) ? settings.rows : [];
        const columns = Array.isArray(settings.columns) ? settings.columns : [];
        const current = getCurrentAnswer(q, state, {});
        const thead = document.createElement('thead');
        const headRow = document.createElement('tr');
        headRow.appendChild(document.createElement('th'));
        columns.forEach(column => {
            const th = document.createElement('th');
            th.textContent = column.label ?? column.id ?? '';
            headRow.appendChild(th);
        });
        thead.appendChild(headRow);
        table.appendChild(thead);

        const tbody = document.createElement('tbody');
        rows.forEach(row => {
            const tr = document.createElement('tr');
            const header = document.createElement('th');
            header.scope = 'row';
            header.textContent = row.label ?? row.id ?? '';
            tr.appendChild(header);

            columns.forEach(column => {
                const td = document.createElement('td');
                const cellType = column.type ?? settings.cellType ?? 'text';
                const cellKey = row.id ?? row.label ?? String(row);
                const colKey = column.id ?? column.label ?? String(column);
                const existingRow = (current && typeof current === 'object') ? current[cellKey] : undefined;
                const cellValue = existingRow && typeof existingRow === 'object' ? existingRow[colKey] : '';
                let input;
                if (cellType === 'number') {
                    input = document.createElement('input');
                    input.type = 'number';
                } else if (cellType === 'boolean' || cellType === 'checkbox') {
                    input = document.createElement('input');
                    input.type = 'checkbox';
                    input.checked = coerceBoolean(cellValue);
                } else {
                    input = document.createElement('input');
                    input.type = 'text';
                    input.value = cellValue ?? '';
                }
                if (cellType !== 'checkbox' && cellType !== 'boolean') {
                    input.value = cellValue ?? '';
                }
                const commit = () => {
                    const draft = { ...(state.answers[q.key] ?? {}) };
                    const rowState = { ...(draft[cellKey] ?? {}) };
                    if (input.type === 'checkbox') {
                        rowState[colKey] = input.checked;
                    } else {
                        rowState[colKey] = input.value;
                    }
                    draft[cellKey] = rowState;
                    state.setAnswer(q.key, draft);
                };
                if (input.type === 'checkbox') {
                    input.defaultChecked = input.checked;
                    input.addEventListener('change', commit);
                } else {
                    input.defaultValue = input.value;
                    input.addEventListener('input', commit);
                }
                td.appendChild(input);
                tr.appendChild(td);
            });
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        return table;
    }

    function fileRenderer(q, state) {
        const wrapper = document.createElement('div');
        const input = document.createElement('input');
        input.type = 'file';
        const settings = q.settings || {};
        if (settings.allowedTypes && Array.isArray(settings.allowedTypes) && settings.allowedTypes.length > 0) {
            input.accept = settings.allowedTypes.join(',');
        }
        if (settings.maxFiles && settings.maxFiles > 1) {
            input.multiple = true;
        }
        const helper = document.createElement('div');
        helper.className = 'file-summary';
        input.addEventListener('change', () => {
            const files = Array.from(input.files ?? []);
            if (files.length === 0) {
                helper.textContent = '';
                state.setAnswer(q.key, []);
                return;
            }
            const parts = files.map(file => `${file.name} (${Math.round(file.size / 1024)} KB)`);
            helper.textContent = parts.join(', ');
            Promise.all(files.map(file => new Promise(resolve => {
                const reader = new FileReader();
                reader.onload = () => {
                    resolve({
                        name: file.name,
                        size: file.size,
                        type: file.type,
                        content: typeof reader.result === 'string' ? reader.result : ''
                    });
                };
                reader.onerror = () => {
                    resolve({ name: file.name, size: file.size, type: file.type, content: '' });
                };
                reader.readAsDataURL(file);
            }))).then(payload => {
                state.setAnswer(q.key, payload);
            });
        });
        if (typeof state.onChange === 'function') {
            state.onChange((key, value) => {
                if (key !== q.key) return;
                if (!Array.isArray(value) || value.length === 0) {
                    helper.textContent = '';
                    input.value = '';
                }
            });
        }
        wrapper.appendChild(input);
        wrapper.appendChild(helper);
        return wrapper;
    }

    function signatureRenderer(q, state) {
        const container = document.createElement('div');
        container.className = 'signature-field';
        const canvas = document.createElement('canvas');
        canvas.width = q.settings && typeof q.settings.width === 'number' ? q.settings.width : 600;
        canvas.height = q.settings && typeof q.settings.height === 'number' ? q.settings.height : 200;
        const ctx = canvas.getContext('2d');
        ctx.lineWidth = q.settings && typeof q.settings.penWidth === 'number' ? q.settings.penWidth : 2;
        ctx.lineCap = 'round';
        const penColor = (q.settings && typeof q.settings.penColor === 'string') ? q.settings.penColor : '#000';
        const backgroundColor = (q.settings && typeof q.settings.backgroundColor === 'string') ? q.settings.backgroundColor : '#fff';
        ctx.fillStyle = backgroundColor;
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.strokeStyle = penColor;

        let drawing = false;
        function pointerPosition(event) {
            const rect = canvas.getBoundingClientRect();
            return {
                x: event.clientX - rect.left,
                y: event.clientY - rect.top
            };
        }

        function startDrawing(event) {
            drawing = true;
            const pos = pointerPosition(event);
            ctx.beginPath();
            ctx.moveTo(pos.x, pos.y);
            event.preventDefault();
        }

        function draw(event) {
            if (!drawing) return;
            const pos = pointerPosition(event);
            ctx.lineTo(pos.x, pos.y);
            ctx.stroke();
            event.preventDefault();
        }

        function endDrawing(event) {
            if (!drawing) return;
            drawing = false;
            ctx.closePath();
            state.setAnswer(q.key, canvas.toDataURL('image/png'));
            event.preventDefault();
        }

        canvas.addEventListener('pointerdown', startDrawing);
        canvas.addEventListener('pointermove', draw);
        canvas.addEventListener('pointerup', endDrawing);
        canvas.addEventListener('pointerleave', endDrawing);

        const clearButton = document.createElement('button');
        clearButton.type = 'button';
        clearButton.textContent = 'Clear signature';
        clearButton.addEventListener('click', () => {
            ctx.fillStyle = backgroundColor;
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            ctx.strokeStyle = penColor;
            state.setAnswer(q.key, null);
        });

        container.appendChild(canvas);
        container.appendChild(clearButton);
        if (typeof state.onChange === 'function') {
            state.onChange((key, value) => {
                if (key !== q.key) return;
                if (!value) {
                    ctx.fillStyle = backgroundColor;
                    ctx.fillRect(0, 0, canvas.width, canvas.height);
                    ctx.strokeStyle = penColor;
                } else if (typeof value === 'string') {
                    const image = new Image();
                    image.onload = () => {
                        ctx.fillStyle = backgroundColor;
                        ctx.fillRect(0, 0, canvas.width, canvas.height);
                        ctx.drawImage(image, 0, 0, canvas.width, canvas.height);
                        ctx.strokeStyle = penColor;
                    };
                    image.src = value;
                }
            });
        }
        return container;
    }

    function staticTextRenderer(q) {
        const p = document.createElement('p');
        const settings = q.settings || {};
        const text = typeof settings.text === 'string' ? settings.text : q.prompt;
        p.textContent = text ?? '';
        if (settings.alignment) {
            p.style.textAlign = settings.alignment;
        }
        return p;
    }

    function staticHtmlRenderer(q) {
        const wrapper = document.createElement('div');
        const settings = q.settings || {};
        if (typeof settings.html === 'string') {
            wrapper.innerHTML = settings.html;
        }
        return wrapper;
    }

    function imageRenderer(q) {
        const settings = q.settings || {};
        const figure = document.createElement('figure');
        const img = document.createElement('img');
        if (settings.url) img.src = settings.url;
        if (settings.alt) img.alt = settings.alt;
        if (settings.width) img.width = Number(settings.width);
        if (settings.height) img.height = Number(settings.height);
        figure.appendChild(img);
        if (settings.caption) {
            const caption = document.createElement('figcaption');
            caption.textContent = settings.caption;
            figure.appendChild(caption);
        }
        return figure;
    }

    function videoRenderer(q) {
        const settings = q.settings || {};
        const video = document.createElement('video');
        video.controls = true;
        if (settings.autoplay) video.autoplay = true;
        if (settings.loop) video.loop = true;
        if (settings.poster) video.poster = settings.poster;
        if (settings.url) {
            const source = document.createElement('source');
            source.src = settings.url;
            video.appendChild(source);
        }
        if (settings.caption) {
            const caption = document.createElement('div');
            caption.textContent = settings.caption;
            const wrapper = document.createElement('div');
            wrapper.appendChild(video);
            wrapper.appendChild(caption);
            return wrapper;
        }
        return video;
    }

    function dividerRenderer(q) {
        const hr = document.createElement('hr');
        const settings = q.settings || {};
        if (settings.color) hr.style.borderColor = settings.color;
        if (settings.thickness) hr.style.borderWidth = `${settings.thickness}px`;
        if (settings.style) hr.style.borderStyle = settings.style;
        return hr;
    }

    function spacerRenderer(q) {
        const settings = q.settings || {};
        const div = document.createElement('div');
        let height = 16;
        if (settings.size === 'sm') height = 8;
        else if (settings.size === 'md') height = 16;
        else if (settings.size === 'lg') height = 32;
        if (settings.customHeight) height = Number(settings.customHeight);
        div.style.height = `${height}px`;
        return div;
    }

    function register(type, renderer, aliases = []) {
        const keys = new Set();
        const record = (key) => {
            if (typeof key !== 'string') return;
            const trimmed = key.trim();
            if (!trimmed) return;
            const lower = trimmed.toLowerCase();
            keys.add(trimmed);
            keys.add(lower);
            keys.add(lower.replace(/[\s-]+/g, '_'));
            keys.add(lower.replace(/[\s_]+/g, '-'));
        };
        record(type);
        ensureArray(aliases).forEach(alias => record(alias));
        keys.forEach(key => {
            if (key) {
                renderers[key] = renderer;
            }
        });
    }

    register('text', createInputRenderer('text'));
    register('textarea', createTextareaRenderer());
    register('number', createInputRenderer('number'));
    register('phone', createInputRenderer('tel'), ['tel']);
    register('boolean', yesNoRenderer, ['yesno']);
    register('email', createInputRenderer('email'));
    register('date', createInputRenderer('date'));
    register('time', createInputRenderer('time'));
    register('single', createChoiceRenderer('single'), ['single-select', 'singlechoice', 'radio']);
    register('multi', createChoiceRenderer('multi'), ['multi-select', 'multiselect', 'checkbox']);
    register('likert', likertRenderer);
    register('nps_0_10', npsRenderer, ['nps', 'nps-0-10', 'nps0-10', 'nps0_10']);
    register('rating_stars', ratingStarsRenderer, ['rating']);
    register('matrix', matrixRenderer);
    register('file', fileRenderer);
    register('signature', signatureRenderer);
    register('static_text', staticTextRenderer);
    register('static_html', staticHtmlRenderer);
    register('image', imageRenderer);
    register('video', videoRenderer);
    register('divider', dividerRenderer);
    register('spacer', spacerRenderer);

    function evaluateSingleCondition(condition, state) {
        if (!condition) return true;
        const emptyMatch = condition.match(/^\s*([A-Za-z0-9_\-]+)\s+is\s+(not\s+)?empty\s*$/i);
        if (emptyMatch) {
            const key = emptyMatch[1];
            const negate = !!emptyMatch[2];
            const value = state.answers[key];
            const hasValue = value !== undefined && value !== null && !(typeof value === 'string' && value.trim() === '') && !(Array.isArray(value) && value.length === 0);
            return negate ? hasValue : !hasValue;
        }

        const comparisonMatch = condition.match(/^\s*([A-Za-z0-9_\-]+)\s*(=|==|!=|>=|<=|>|<|contains|notcontains|in|notin)\s*(.+)\s*$/i);
        if (!comparisonMatch) return true;

        const key = comparisonMatch[1];
        const op = comparisonMatch[2].toLowerCase();
        let rawValue = comparisonMatch[3].trim();
        let comparisonValue;
        if ((rawValue.startsWith('"') && rawValue.endsWith('"')) || (rawValue.startsWith("'") && rawValue.endsWith("'"))) {
            comparisonValue = rawValue.substring(1, rawValue.length - 1);
        } else if (rawValue.startsWith('[') && rawValue.endsWith(']')) {
            const inner = rawValue.substring(1, rawValue.length - 1);
            comparisonValue = inner.split(',').map(part => part.trim().replace(/^['"]|['"]$/g, ''));
        } else if (!Number.isNaN(Number(rawValue))) {
            comparisonValue = Number(rawValue);
        } else if (rawValue.toLowerCase() === 'true' || rawValue.toLowerCase() === 'false') {
            comparisonValue = rawValue.toLowerCase() === 'true';
        } else {
            comparisonValue = rawValue;
        }

        const answer = state.answers[key];
        const normalizedAnswer = Array.isArray(answer)
            ? answer
            : (typeof answer === 'string' ? answer.trim() : answer);

        switch (op) {
            case '=':
            case '==':
                return String(normalizedAnswer) === String(comparisonValue);
            case '!=':
                return String(normalizedAnswer) !== String(comparisonValue);
            case '>':
                return Number(normalizedAnswer) > Number(comparisonValue);
            case '>=':
                return Number(normalizedAnswer) >= Number(comparisonValue);
            case '<':
                return Number(normalizedAnswer) < Number(comparisonValue);
            case '<=':
                return Number(normalizedAnswer) <= Number(comparisonValue);
            case 'contains':
                if (Array.isArray(normalizedAnswer)) {
                    return normalizedAnswer.some(v => String(v) === String(comparisonValue));
                }
                if (typeof normalizedAnswer === 'string') {
                    return normalizedAnswer.includes(String(comparisonValue));
                }
                return false;
            case 'notcontains':
                if (Array.isArray(normalizedAnswer)) {
                    return !normalizedAnswer.some(v => String(v) === String(comparisonValue));
                }
                if (typeof normalizedAnswer === 'string') {
                    return !normalizedAnswer.includes(String(comparisonValue));
                }
                return true;
            case 'in':
                {
                    const list = ensureArray(comparisonValue);
                    if (Array.isArray(normalizedAnswer)) {
                        return normalizedAnswer.some(value => list.map(String).includes(String(value)));
                    }
                    return list.map(String).includes(String(normalizedAnswer));
                }
            case 'notin':
                {
                    const list = ensureArray(comparisonValue);
                    if (Array.isArray(normalizedAnswer)) {
                        return normalizedAnswer.every(value => !list.map(String).includes(String(value)));
                    }
                    return !list.map(String).includes(String(normalizedAnswer));
                }
            default:
                return true;
        }
    }

    function evaluateVisible(condition, state) {
        if (!condition || !condition.trim()) return true;
        const segments = condition.split(/\s+(and|&&|or|\|\|)\s+/i);
        let result = null;
        let pendingOp = null;
        for (let i = 0; i < segments.length; i += 1) {
            const segment = segments[i];
            if (/^(and|&&|or|\|\|)$/i.test(segment)) {
                pendingOp = segment.toLowerCase();
                continue;
            }
            const evaluation = evaluateSingleCondition(segment, state);
            if (result === null) {
                result = evaluation;
            } else if (pendingOp === 'and' || pendingOp === '&&') {
                result = result && evaluation;
            } else if (pendingOp === 'or' || pendingOp === '||') {
                result = result || evaluation;
            } else {
                result = evaluation;
            }
        }
        return result === null ? true : result;
    }

    function hasValue(value) {
        if (value === null || value === undefined) return false;
        if (typeof value === 'string') return value.trim().length > 0;
        if (Array.isArray(value)) return value.length > 0;
        if (typeof value === 'object') return Object.keys(value).length > 0;
        return true;
    }

    function parseValidations(settings) {
        const validations = [];
        if (!settings) return validations;
        const raw = settings.validations;
        if (Array.isArray(raw)) {
            raw.forEach(rule => {
                if (typeof rule === 'string') {
                    const [name, ...rest] = rule.split(':');
                    validations.push({ name: name.trim().toLowerCase(), value: rest.join(':') });
                } else if (rule && typeof rule === 'object' && typeof rule.name === 'string') {
                    validations.push({ name: rule.name.trim().toLowerCase(), value: rule.value });
                }
            });
        }
        return validations;
    }

    function validateQuestion(q, state) {
        const value = state.answers[q.key];
        const errors = [];
        const type = q.type;

        if (q.required) {
            if (type === 'file') {
                if (!Array.isArray(value) || value.length === 0) {
                    errors.push('This field is required.');
                }
            } else if (!hasValue(value)) {
                errors.push('This field is required.');
            }
        }

        if (value === null || value === undefined || value === '') {
            return errors;
        }

        switch (type) {
            case 'number': {
                const numberValue = Number(value);
                if (Number.isNaN(numberValue)) errors.push('Enter a valid number.');
                break;
            }
            case 'email': {
                const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
                if (typeof value !== 'string' || !emailPattern.test(value.trim())) {
                    errors.push('Enter a valid email address.');
                }
                break;
            }
            case 'phone': {
                const digits = typeof value === 'string' ? value.replace(/\D+/g, '') : '';
                if (digits.length < 7) {
                    errors.push('Enter a valid phone number.');
                }
                break;
            }
            case 'date': {
                if (Number.isNaN(Date.parse(value))) {
                    errors.push('Enter a valid date.');
                }
                break;
            }
            case 'time': {
                if (typeof value !== 'string' || !/^([01]?\d|2[0-3]):[0-5]\d$/.test(value)) {
                    errors.push('Enter a valid time.');
                }
                break;
            }
            case 'nps_0_10': {
                const numeric = Number(value);
                if (Number.isNaN(numeric) || numeric < 0 || numeric > 10) {
                    errors.push('Select a score between 0 and 10.');
                }
                break;
            }
            case 'rating_stars': {
                const numeric = Number(value);
                const limit = q.settings && typeof q.settings.stars === 'number' ? q.settings.stars : 5;
                if (Number.isNaN(numeric) || numeric < 1 || numeric > limit) {
                    errors.push('Select a rating.');
                }
                break;
            }
            case 'matrix': {
                if (q.required) {
                    const rows = q.settings && Array.isArray(q.settings.rows) ? q.settings.rows : [];
                    const columns = q.settings && Array.isArray(q.settings.columns) ? q.settings.columns : [];
                    const valueRows = value && typeof value === 'object' ? Object.keys(value) : [];
                    if (rows.length > 0 && columns.length > 0) {
                        const allFilled = rows.every(row => {
                            const rowId = row.id ?? row.label;
                            const rowValue = value[rowId];
                            if (!rowValue || typeof rowValue !== 'object') return false;
                            return columns.every(column => {
                                const colId = column.id ?? column.label;
                                const cellValue = rowValue[colId];
                                return hasValue(cellValue) || column.type === 'checkbox' || column.type === 'boolean';
                            });
                        });
                        if (!allFilled) {
                            errors.push('Complete the entire matrix.');
                        }
                    } else if (valueRows.length === 0) {
                        errors.push('Complete the matrix.');
                    }
                }
                break;
            }
            case 'file': {
                if (Array.isArray(value)) {
                    const settings = q.settings || {};
                    const maxSize = settings.maxFileSize ? Number(settings.maxFileSize) : null;
                    const allowedTypes = Array.isArray(settings.allowedTypes) ? settings.allowedTypes : [];
                    value.forEach(file => {
                        if (maxSize && file.size > maxSize) {
                            errors.push(`${file.name} is too large.`);
                        }
                        if (allowedTypes.length > 0) {
                            const matches = allowedTypes.some(pattern => {
                                if (pattern.endsWith('/*')) {
                                    const prefix = pattern.slice(0, -1);
                                    return file.type.startsWith(prefix);
                                }
                                return file.type === pattern;
                            });
                            if (!matches) {
                                errors.push(`${file.name} has an invalid type.`);
                            }
                        }
                    });
                    if (settings.maxFiles && value.length > Number(settings.maxFiles)) {
                        errors.push(`Select at most ${settings.maxFiles} files.`);
                    }
                }
                break;
            }
            default:
                break;
        }

        const validations = parseValidations(q.settings);
        validations.forEach(rule => {
            const compareValue = typeof rule.value === 'string' ? rule.value.trim() : rule.value;
            switch (rule.name) {
                case 'min': {
                    const numeric = Number(value);
                    if (Number.isNaN(numeric) || numeric < Number(compareValue)) {
                        errors.push(`Value must be at least ${compareValue}.`);
                    }
                    break;
                }
                case 'max': {
                    const numeric = Number(value);
                    if (Number.isNaN(numeric) || numeric > Number(compareValue)) {
                        errors.push(`Value must be at most ${compareValue}.`);
                    }
                    break;
                }
                case 'minlength': {
                    const length = typeof value === 'string' ? value.length : ensureArray(value).length;
                    if (length < Number(compareValue)) {
                        errors.push(`Enter at least ${compareValue} characters.`);
                    }
                    break;
                }
                case 'maxlength': {
                    const length = typeof value === 'string' ? value.length : ensureArray(value).length;
                    if (length > Number(compareValue)) {
                        errors.push(`Enter no more than ${compareValue} characters.`);
                    }
                    break;
                }
                case 'pattern': {
                    if (typeof compareValue === 'string' && compareValue) {
                        const regex = new RegExp(compareValue);
                        if (!regex.test(String(value))) {
                            errors.push('Value does not match the required pattern.');
                        }
                    }
                    break;
                }
                default:
                    break;
            }
        });

        return errors;
    }

    function validate(questions, state) {
        const result = { valid: true, errors: {} };
        questions.forEach(q => {
            if (!evaluateVisible(q.visibleIf, state)) {
                delete state.errors[q.key];
                return;
            }
            const errors = validateQuestion(q, state);
            if (errors.length > 0) {
                result.valid = false;
                result.errors[q.key] = errors;
            }
        });
        state.errors = result.errors;
        return result;
    }

    function renderQuestion(q, state) {
        const type = typeof q.type === 'string' ? q.type : '';
        const normalized = type.trim().toLowerCase();
        const renderer = renderers[type]
            || renderers[normalized]
            || renderers[normalized.replace(/[\s-]+/g, '_')]
            || renderers[normalized.replace(/[\s_]+/g, '-')];
        if (!renderer) {
            const fallback = document.createElement('div');
            fallback.textContent = `Unsupported question type: ${q.type}`;
            return fallback;
        }
        return renderer(q, state);
    }

    global.SurveyRenderer = {
        renderers,
        renderQuestion,
        evaluateVisible,
        validate
    };
})(window);
