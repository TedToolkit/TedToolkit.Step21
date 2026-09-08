(function (root, factory) {
    "use strict";
    var api = factory();
    if (typeof module === "object" && module && module.exports) {
        module.exports = api;
    }
    root.P21 = api;
}(typeof globalThis === "object" ? globalThis : this, function () {
    "use strict";

    var bridgeFormatVersion = 1;
    var modelRecordName = "[P21.Model]";
    var populationRecordName = "[P21.Population]";

    function fail(message) {
        throw new TypeError(message);
    }

    function requireFiniteNumber(value, name) {
        if (typeof value !== "number" || !isFinite(value)) {
            fail(name + " must be a finite ECMAScript number.");
        }
        return value;
    }

    function leftPad(value, length) {
        var result = String(value);
        while (result.length < length) {
            result = "0" + result;
        }
        return result;
    }

    function expandInteger(value) {
        var text = String(value);
        var marker = text.search(/[eE]/);
        if (marker < 0) {
            return text;
        }
        var mantissa = text.substring(0, marker);
        var exponent = Number(text.substring(marker + 1));
        var negative = mantissa.charAt(0) === "-";
        if (negative) {
            mantissa = mantissa.substring(1);
        }
        var point = mantissa.indexOf(".");
        var digits = mantissa.replace(".", "");
        var decimal = point < 0 ? digits.length : point;
        var position = decimal + exponent;
        if (position <= 0) {
            digits = new Array(1 - position).join("0") + digits;
            position = 1;
        }
        if (position < digits.length) {
            fail("P21.Integer cannot represent a non-integral ECMAScript number.");
        }
        digits += new Array(position - digits.length + 1).join("0");
        return (negative ? "-" : "") + digits;
    }

    function realP21(value) {
        var text = String(value);
        var marker = text.search(/[eE]/);
        if (marker < 0) {
            return text.indexOf(".") < 0 ? text + "." : text;
        }
        var mantissa = text.substring(0, marker);
        if (mantissa.indexOf(".") < 0) {
            mantissa += ".";
        }
        var exponent = Number(text.substring(marker + 1));
        return mantissa + "E" + String(exponent);
    }

    function stringP21(value) {
        var result = "'";
        var index;
        for (index = 0; index < value.length; index += 1) {
            var code = value.charCodeAt(index);
            if (code === 39) {
                result += "''";
            } else if (code === 92) {
                result += "\\\\";
            } else if (code >= 32 && code <= 126) {
                result += value.charAt(index);
            } else if (code >= 0xD800 && code <= 0xDBFF && index + 1 < value.length) {
                var low = value.charCodeAt(index + 1);
                if (low < 0xDC00 || low > 0xDFFF) {
                    fail("P21.String contains an unpaired high surrogate.");
                }
                var scalar = 0x10000 + ((code - 0xD800) * 0x400) + low - 0xDC00;
                result += "\\X4\\" + leftPad(scalar.toString(16).toUpperCase(), 8) + "\\X0\\";
                index += 1;
            } else if (code >= 0xD800 && code <= 0xDFFF) {
                fail("P21.String contains an unpaired surrogate.");
            } else {
                result += "\\X2\\" + leftPad(code.toString(16).toUpperCase(), 4) + "\\X0\\";
            }
        }
        return result + "'";
    }

    function requireName(value, pattern, description) {
        if (typeof value !== "string" || !pattern.test(value)) {
            fail(description + " is invalid.");
        }
        return value;
    }

    function Wrapper() {
    }
    Wrapper.prototype.valueOf = function () {
        return null;
    };
    Wrapper.prototype.toString = function () {
        return String(this.valueOf());
    };
    Wrapper.prototype.toP21String = function () {
        return "$";
    };

    function inherit(type) {
        type.prototype = Object.create(Wrapper.prototype);
        type.prototype.constructor = type;
    }

    function Integer(value, p21) {
        this._value = requireFiniteNumber(Number(value), "P21.Integer value");
        if (Math.floor(this._value) !== this._value) {
            fail("P21.Integer value must be integral.");
        }
        this._p21 = p21 === undefined ? expandInteger(this._value) : String(p21);
        if (!/^-?(?:0|[1-9][0-9]*)$/.test(this._p21)) {
            fail("P21.Integer spelling is invalid.");
        }
    }
    inherit(Integer);
    Integer.prototype.valueOf = function () { return this._value; };
    Integer.prototype.toString = function () { return String(this._value); };
    Integer.prototype.toP21String = function () { return this._p21; };

    function Real(value, p21) {
        this._value = requireFiniteNumber(Number(value), "P21.Real value");
        this._p21 = p21 === undefined ? realP21(this._value) : String(p21);
        if (!/^-?[0-9]+\.[0-9]*(?:E[+-]?[0-9]+)?$/.test(this._p21)) {
            fail("P21.Real spelling is invalid.");
        }
    }
    inherit(Real);
    Real.prototype.valueOf = function () { return this._value; };
    Real.prototype.toString = function () { return String(this._value); };
    Real.prototype.toP21String = function () { return this._p21; };

    function P21String(value) {
        if (typeof value !== "string") {
            fail("P21.String value must be an ECMAScript string.");
        }
        this._value = value;
    }
    inherit(P21String);
    P21String.prototype.valueOf = function () { return this._value; };
    P21String.prototype.toString = function () { return this._value; };
    P21String.prototype.toP21String = function () { return stringP21(this._value); };

    function Enumeration(value) {
        if (value === true) {
            this._value = true;
            this._symbol = "T";
        } else if (value === false) {
            this._value = false;
            this._symbol = "F";
        } else {
            this._symbol = requireName(value, /^[A-Z_][A-Z0-9_]*$/, "P21.Enumeration value");
            this._value = this._symbol === "T" ? true : this._symbol === "F" ? false : this._symbol;
        }
    }
    inherit(Enumeration);
    Enumeration.prototype.valueOf = function () { return this._value; };
    Enumeration.prototype.toString = function () { return String(this._value); };
    Enumeration.prototype.toP21String = function () { return "." + this._symbol + "."; };

    function Binary(value) {
        this._value = requireName(value, /^[0-3][0-9A-F]*$/, "P21.Binary value");
        if (Number(this._value.charAt(0)) > (this._value.length - 1) * 4) {
            fail("P21.Binary unused-bit count exceeds the encoded bit count.");
        }
    }
    inherit(Binary);
    Binary.prototype.valueOf = function () { return this._value; };
    Binary.prototype.toString = function () { return this._value; };
    Binary.prototype.toP21String = function () { return "\"" + this._value + "\""; };

    function referenceType(prefix, resolverName, constant) {
        function Reference(value, host) {
            this._name = requireName(
                value,
                constant ? /^[A-Z_][A-Z0-9_]*$/ : /^[1-9][0-9]*$/,
                "P21 reference name");
            this._host = host || null;
        }
        inherit(Reference);
        Reference.prototype.valueOf = function () {
            return this._host && typeof this._host[resolverName] === "function"
                ? this._host[resolverName](this._name)
                : null;
        };
        Reference.prototype.toString = function () { return this._name; };
        Reference.prototype.toP21String = function () { return prefix + this._name; };
        return Reference;
    }

    var EID = referenceType("#", "resolveEntity", false);
    var VID = referenceType("@", "resolveValue", false);
    var CIN = referenceType("#", "resolveConstantEntity", true);
    var CVN = referenceType("@", "resolveConstantValue", true);

    function List() {
        this._values = Array.prototype.slice.call(arguments);
        this._values.forEach(function (value) {
            if (value !== null && !(value instanceof Wrapper)) {
                fail("P21.List members must be P21.Wrapper objects or null.");
            }
        });
    }
    inherit(List);
    List.prototype.valueOf = function () {
        return this._values.map(function (value) { return value === null ? null : value.valueOf(); });
    };
    List.prototype.toString = function () {
        return this._values.map(function (value) { return value === null ? null : value.toString(); });
    };
    List.prototype.toP21String = function () {
        return this._values.map(function (value) { return value === null ? "$" : value.toP21String(); });
    };

    function isUriScheme(value) {
        return /^[A-Za-z][A-Za-z0-9+.-]*$/.test(value);
    }

    function validURI(value) {
        if (typeof value !== "string" || value.length === 0 || /[<>\s]/.test(value)) {
            return false;
        }
        var hash = value.indexOf("#");
        if (hash >= 0 && value.indexOf("#", hash + 1) >= 0) {
            return false;
        }
        var allowed = /^[A-Za-z0-9\-_.!~*'();/?:@&=+$,#%]+$/;
        if (!allowed.test(value)) {
            return false;
        }
        var index;
        for (index = 0; index < value.length; index += 1) {
            if (value.charAt(index) === "%") {
                if (!/^[0-9A-Fa-f]{2}$/.test(value.substring(index + 1, index + 3))) {
                    return false;
                }
                index += 2;
            }
        }

        if (hash === 0) {
            return true;
        }

        var resourceLength = hash >= 0 ? hash : value.length;
        var resource = value.substring(0, resourceLength);
        var colon = resource.indexOf(":");
        var slash = resource.indexOf("/");
        if (colon > 0 && (slash < 0 || colon < slash) && isUriScheme(resource.substring(0, colon))) {
            return colon + 1 < resourceLength;
        }

        var query = resource.indexOf("?");
        var path = query >= 0 ? resource.substring(0, query) : resource;
        if (path.length === 0) {
            return false;
        }
        var pathSlash = path.indexOf("/");
        var firstSegment = pathSlash >= 0 ? path.substring(0, pathSlash) : path;
        if (firstSegment.length === 0 && path.charAt(0) !== "/") {
            return false;
        }
        if (firstSegment.indexOf(":") >= 0) {
            return false;
        }
        return true;
    }

    function URI(value, host) {
        if (!validURI(value)) {
            fail("P21.URI value is invalid.");
        }
        this._value = value;
        this._host = host || null;
    }
    inherit(URI);
    URI.prototype.valueOf = function () {
        return this._host && typeof this._host.resolveURI === "function"
            ? this._host.resolveURI(this._value)
            : null;
    };
    URI.prototype.toString = function () { return this._value; };
    URI.prototype.toP21String = function () { return "<" + this._value + ">"; };

    function fromNode(node, host) {
        if (!node || typeof node !== "object") {
            fail("Annex F bridge values must be objects.");
        }
        switch (node.kind) {
        case "null": return null;
        case "integer": return new Integer(Number(node.p21), node.p21);
        case "real": return new Real(Number(node.p21.replace("E", "e")), node.p21);
        case "string": return new P21String(node.value);
        case "enumeration": return new Enumeration(node.value);
        case "binary": return new Binary(node.value);
        case "eid": return new EID(node.name, host);
        case "vid": return new VID(node.name, host);
        case "cin": return new CIN(node.name, host);
        case "cvn": return new CVN(node.name, host);
        case "list":
            return new (Function.prototype.bind.apply(List, [null].concat(node.values.map(function (value) {
                return fromNode(value, host);
            }))));
        case "uri": return new URI(node.value, host);
        default: fail("Unknown Annex F bridge value kind '" + node.kind + "'.");
        }
    }

    function toNode(value) {
        if (value === null) { return { kind: "null" }; }
        if (!(value instanceof Wrapper)) { fail("Annex F values must be P21.Wrapper objects or null."); }
        if (value instanceof Integer) { return { kind: "integer", p21: value.toP21String() }; }
        if (value instanceof Real) { return { kind: "real", p21: value.toP21String() }; }
        if (value instanceof P21String) { return { kind: "string", value: value.valueOf() }; }
        if (value instanceof Enumeration) { return { kind: "enumeration", value: value._symbol }; }
        if (value instanceof Binary) { return { kind: "binary", value: value.valueOf() }; }
        if (value instanceof EID) { return { kind: "eid", name: value.toString() }; }
        if (value instanceof VID) { return { kind: "vid", name: value.toString() }; }
        if (value instanceof CIN) { return { kind: "cin", name: value.toString() }; }
        if (value instanceof CVN) { return { kind: "cvn", name: value.toString() }; }
        if (value instanceof List) { return { kind: "list", values: value._values.map(toNode) }; }
        if (value instanceof URI) { return { kind: "uri", value: value.toString() }; }
        fail("Unknown P21.Wrapper subtype.");
    }

    function copyState(state) {
        return JSON.parse(JSON.stringify(state));
    }

    function requireBridgeState(state) {
        if (!state || state.formatVersion !== bridgeFormatVersion || !Array.isArray(state.anchors)
                || !Array.isArray(state.schemaPopulation)) {
            fail("The caller-supplied Annex F bridge snapshot is invalid or unsupported.");
        }
        return state;
    }

    function modelRecord(model) {
        if (!model || !Object.prototype.hasOwnProperty.call(model, modelRecordName)) {
            fail("P21.Model is not owned by this P21 module.");
        }
        return model[modelRecordName];
    }

    function populationRecord(population) {
        if (!population || !Object.prototype.hasOwnProperty.call(population, populationRecordName)) {
            fail("P21.Population is not owned by this P21 module.");
        }
        return population[populationRecordName];
    }

    function populationState(population) {
        var record = populationRecord(population);
        return record.model === null
            ? record.state
            : modelRecord(record.model).state.schemaPopulation[record.index];
    }

    function attachPopulations(model, record) {
        record.populations = record.state.schemaPopulation.map(function (population, index) {
            return new Population(population, model, index);
        });
    }

    function applyModel(model, mutation) {
        var record = modelRecord(model);
        var candidate = copyState(record.state);
        mutation(candidate);
        var canonical = requireBridgeState(record.host.apply(copyState(candidate)));
        record.state = copyState(canonical);
        attachPopulations(model, record);
    }

    function readAnchorValue(model, anchorIndex) {
        return fromNode(modelRecord(model).state.anchors[anchorIndex].value, modelRecord(model).host);
    }

    function writeAnchorValue(model, anchorIndex, value) {
        applyModel(model, function (state) {
            state.anchors[anchorIndex].value = toNode(value);
        });
    }

    function readTagValue(model, anchorIndex, tagIndex) {
        var record = modelRecord(model);
        return fromNode(record.state.anchors[anchorIndex].tags[tagIndex].value, record.host);
    }

    function writeTagValue(model, anchorIndex, tagIndex, value) {
        applyModel(model, function (state) {
            state.anchors[anchorIndex].tags[tagIndex].value = toNode(value);
        });
    }

    function AnchorPropertyCollision(model, anchorIndex, tagIndex) {
        Object.defineProperty(this, "anchorValue", {
            enumerable: true,
            get: function () { return readAnchorValue(model, anchorIndex); },
            set: function (value) { writeAnchorValue(model, anchorIndex, value); }
        });
        Object.defineProperty(this, "tagValue", {
            enumerable: true,
            get: function () { return readTagValue(model, anchorIndex, tagIndex); },
            set: function (value) { writeTagValue(model, anchorIndex, tagIndex, value); }
        });
    }
    AnchorPropertyCollision.prototype.valueOf = function () {
        var value = this.anchorValue;
        return value === null ? null : value.valueOf();
    };
    AnchorPropertyCollision.prototype.toString = function () {
        var value = this.anchorValue;
        return value === null ? "null" : value.toString();
    };
    AnchorPropertyCollision.prototype.toP21String = function () {
        var value = this.anchorValue;
        return value === null ? "$" : value.toP21String();
    };

    var modelMethodNames = {
        uri: true,
        name: true,
        schema_population: true,
        set_uri: true,
        set_name: true,
        set_schema_population: true
    };

    function Anchor(model, anchorIndex) {
        var state = modelRecord(model).state.anchors[anchorIndex];
        var view = modelMethodNames[state.name]
            ? function () { return Model.prototype[state.name].apply(model, arguments); }
            : {};
        var valueTagIndex = -1;
        state.tags.forEach(function (tag, tagIndex) {
            if (tag.name === "value") {
                valueTagIndex = tagIndex;
            }
        });
        if (valueTagIndex >= 0) {
            var collision = new AnchorPropertyCollision(model, anchorIndex, valueTagIndex);
            Object.defineProperty(view, "$value", {
                enumerable: true,
                configurable: false,
                get: function () { return collision; },
                set: function (value) { writeAnchorValue(model, anchorIndex, value); }
            });
        } else {
            Object.defineProperty(view, "$value", {
                enumerable: true,
                configurable: false,
                get: function () { return readAnchorValue(model, anchorIndex); },
                set: function (value) { writeAnchorValue(model, anchorIndex, value); }
            });
        }
        state.tags.forEach(function (tag, tagIndex) {
            if (tag.name === "value") {
                return;
            }
            Object.defineProperty(view, "$" + tag.name, {
                enumerable: true,
                configurable: false,
                get: function () { return readTagValue(model, anchorIndex, tagIndex); },
                set: function (value) { writeTagValue(model, anchorIndex, tagIndex, value); }
            });
        });
        return view;
    }

    function Population(state, model, index) {
        Object.defineProperty(this, populationRecordName, {
            enumerable: false,
            configurable: false,
            writable: false,
            value: {
                state: state || { uri: "#", stamp: null, messageDigest: null, verification: false },
                model: model || null,
                index: index === undefined ? -1 : index
            }
        });
    }
    Population.prototype.uri = function () {
        var record = populationRecord(this);
        return new URI(populationState(this).uri, record.model === null ? null : modelRecord(record.model).host);
    };
    Population.prototype.stamp = function () {
        var stamp = populationState(this).stamp;
        return stamp === null ? null : new Date(stamp);
    };
    Population.prototype.verification = function () { return populationState(this).verification === true; };
    Population.prototype.set_uri = function (value) {
        if (!(value instanceof URI)) { fail("P21.Population.set_uri requires a P21.URI."); }
        var record = populationRecord(this);
        if (record.model === null) {
            record.state.uri = value.toString();
            record.state.messageDigest = null;
            record.state.verification = false;
            return;
        }
        applyModel(record.model, function (state) {
            var population = state.schemaPopulation[record.index];
            population.uri = value.toString();
            population.messageDigest = null;
            population.verification = false;
        });
    };
    Population.prototype.set_stamp = function (value) {
        if (!(value instanceof Date) || isNaN(value.valueOf())) {
            fail("P21.Population.set_stamp requires a valid ECMAScript Date.");
        }
        var record = populationRecord(this);
        if (record.model === null) {
            record.state.stamp = value.toISOString();
            record.state.messageDigest = null;
            record.state.verification = false;
            return;
        }
        applyModel(record.model, function (state) {
            var population = state.schemaPopulation[record.index];
            population.stamp = value.toISOString();
            population.messageDigest = null;
            population.verification = false;
        });
    };
    Population.prototype.set_verification = function () {
        var record = populationRecord(this);
        if (record.model === null || typeof modelRecord(record.model).host.generateVerification !== "function") {
            fail("P21.Population.set_verification requires the caller-supplied generateVerification capability.");
        }
        var digest = modelRecord(record.model).host.generateVerification(this);
        if (typeof digest !== "string" || digest.length === 0
                || !/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(digest)) {
            fail("The generated schema-population verification must be canonical Base64.");
        }
        applyModel(record.model, function (state) {
            state.schemaPopulation[record.index].messageDigest = digest;
            state.schemaPopulation[record.index].verification = true;
        });
    };

    function Model(host) {
        if (!host || typeof host.snapshot !== "function" || typeof host.apply !== "function") {
            fail("P21.Model requires a caller-supplied host with snapshot() and transactional apply().");
        }
        var record = { host: host, state: copyState(requireBridgeState(host.snapshot())), populations: [] };
        Object.defineProperty(this, modelRecordName, {
            enumerable: false,
            configurable: false,
            writable: false,
            value: record
        });
        attachPopulations(this, record);
        record.state.anchors.forEach(function (anchor, anchorIndex) {
            Object.defineProperty(this, anchor.name, {
                enumerable: true,
                configurable: false,
                writable: false,
                value: new Anchor(this, anchorIndex)
            });
        }, this);
    }
    Model.prototype.uri = function () {
        var record = modelRecord(this);
        return new URI(record.state.uri, record.host);
    };
    Model.prototype.name = function () { return new P21String(modelRecord(this).state.name); };
    Model.prototype.schema_population = function () { return modelRecord(this).populations.slice(); };
    Model.prototype.set_uri = function (value) {
        if (!(value instanceof URI)) { fail("P21.Model.set_uri requires a P21.URI."); }
        applyModel(this, function (state) { state.uri = value.toString(); });
    };
    Model.prototype.set_name = function (value) {
        if (!(value instanceof P21String)) { fail("P21.Model.set_name requires a P21.String."); }
        applyModel(this, function (state) { state.name = value.valueOf(); });
    };
    Model.prototype.set_schema_population = function (values) {
        if (!Array.isArray(values) || values.some(function (value) { return !(value instanceof Population); })) {
            fail("P21.Model.set_schema_population requires an array of P21.Population objects.");
        }
        applyModel(this, function (state) {
            state.schemaPopulation = values.map(function (value) { return copyState(populationState(value)); });
        });
    };

    return Object.freeze({
        bridgeFormatVersion: bridgeFormatVersion,
        Wrapper: Wrapper,
        Integer: Integer,
        Real: Real,
        String: P21String,
        Enumeration: Enumeration,
        Binary: Binary,
        EID: EID,
        VID: VID,
        CIN: CIN,
        CVN: CVN,
        List: List,
        URI: URI,
        Population: Population,
        Model: Model
    });
}));
