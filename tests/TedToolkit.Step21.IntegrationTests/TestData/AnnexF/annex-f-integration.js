"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");

const P21 = require(process.argv[2]);
const initial = JSON.parse(fs.readFileSync(process.argv[3], "utf8"));
let latest = initial;
let assertions = 0;
const covered = new Set();
const manifest = JSON.parse(fs.readFileSync(process.argv[5], "utf8"));

function check(requirement, action) {
    if (typeof requirement === "function") {
        action = requirement;
    } else {
        covered.add(requirement);
    }
    action();
    assertions += 1;
}

const host = {
    snapshot: () => latest,
    apply: state => { latest = state; return latest; },
    resolveEntity: name => ({ type: "entity", name }),
    resolveValue: name => ({ type: "value", name }),
    resolveConstantEntity: name => ({ type: "constant-entity", name }),
    resolveConstantValue: name => ({ type: "constant-value", name }),
    resolveURI: uri => uri === "not-an-exchange.bin" ? null : ({ type: "uri", uri }),
    generateVerification: () => "AQID"
};

const model = new P21.Model(host);

check(() => assert.equal(P21.bridgeFormatVersion, 1));
check("F.2.anchor-property", () => assert.equal(Object.hasOwn(model, "integer"), true));
check("F.3.value-property", () => assert.equal(model.integer.$value instanceof P21.Integer, true));
check("F.3.1", () => assert.equal(model.integer.$value.valueOf(), 10));
check(() => assert.equal(model.integer.$value.toString(), "10"));
check(() => assert.equal(model.integer.$value.toP21String(), "10"));
check("F.3.2", () => assert.equal(model.real.$value.valueOf(), 1.25));
check(() => assert.equal(model.real.$value.toString(), "1.25"));
check(() => assert.equal(model.real.$value.toP21String().includes("."), true));
check("F.3.3", () => assert.equal(model.text.$value.valueOf(), "hé'llo\\"));
check(() => assert.equal(model.text.$value.toString(), "hé'llo\\"));
check(() => assert.equal(model.text.$value.toP21String(), "'h\\X2\\00E9\\X0\\''llo\\\\'"));
check(() => assert.throws(() => new P21.String("\uD800").toP21String(), /unpaired/));
check(() => assert.throws(() => new P21.String("\uDC00").toP21String(), /unpaired/));
check("F.3.4", () => assert.equal(new P21.Enumeration(true).valueOf(), true));
check(() => assert.equal(new P21.Enumeration(false).valueOf(), false));
check(() => assert.equal(new P21.Enumeration(true).toString(), "true"));
check(() => assert.equal(new P21.Enumeration(false).toP21String(), ".F."));
check(() => assert.equal(new P21.Enumeration("RED").valueOf(), "RED"));
check(() => assert.equal(new P21.Enumeration("RED").toP21String(), ".RED."));
check("F.3.5", () => assert.equal(model.binary.$value.valueOf(), "15"));
check(() => assert.equal(model.binary.$value.toString(), "15"));
check(() => assert.equal(model.binary.$value.toP21String(), "\"15\""));
check(() => assert.throws(() => new P21.Binary("3F"), /left-fill/));
check("F.3.6", () => assert.deepEqual(model.eid.$value.valueOf(), { type: "entity", name: "20" }));
check(() => assert.equal(model.eid.$value.toString(), "20"));
check(() => assert.equal(model.eid.$value.toP21String(), "#20"));
check("F.3.7", () => assert.deepEqual(model.vid.$value.valueOf(), { type: "value", name: "30" }));
check(() => assert.equal(model.vid.$value.toString(), "30"));
check(() => assert.equal(model.vid.$value.toP21String(), "@30"));
check("F.3.8", () => assert.deepEqual(model.cin.$value.valueOf(), { type: "constant-entity", name: "INCH" }));
check(() => assert.equal(model.cin.$value.toString(), "INCH"));
check(() => assert.equal(model.cin.$value.toP21String(), "#INCH"));
check("F.3.9", () => assert.deepEqual(model.cvn.$value.valueOf(), { type: "constant-value", name: "PI" }));
check(() => assert.equal(model.cvn.$value.toString(), "PI"));
check(() => assert.equal(model.cvn.$value.toP21String(), "@PI"));
check("F.3.10", () => assert.equal(model["null-value"].$value, null));
check("F.3.11", () => assert.deepEqual(model.list.$value.valueOf(), [1, "member", null]));
check(() => assert.deepEqual(model.list.$value.toString(), ["1", "member", null]));
check(() => assert.deepEqual(model.list.$value.toP21String(), ["1", "'member'", "$" ]));
check("F.3.12", () => assert.deepEqual(model.resource.$value.valueOf(), { type: "uri", uri: "#integer" }));
check(() => assert.equal(model.resource.$value.toString(), "#integer"));
check(() => assert.equal(model.resource.$value.toP21String(), "<#integer>"));
check(() => assert.throws(() => new P21.URI("1:relative"), /invalid/));
check(() => assert.equal(new P21.URI("foo:?opaque").toP21String(), "<foo:?opaque>"));
check(() => assert.equal(new P21.URI("not-an-exchange.bin", host).valueOf(), null));
check("F.3.tag-properties", () => assert.equal(model.tagged.$label.valueOf(), "original"));
check("F.3.wrapper", () => assert.equal(new P21.Wrapper().toP21String(), "$"));
check("F.4.1", () => assert.equal(model.uri().toP21String(), "<https://example.test/original.step>"));
check("F.4.2", () => assert.equal(model.name().valueOf(), "annex-f.original"));

const initialPopulation = model.schema_population()[0];
check("F.4.3", () => assert.equal(initialPopulation.uri().toString(), "https://example.test/population.step"));
check(() => assert.equal(initialPopulation.stamp() instanceof Date, true));
check(() => assert.equal(initialPopulation.verification(), true));

model.integer.$value = new P21.Integer(42);
model.real.$value = new P21.Real(10);
model.text.$value = new P21.String("changed ' text");
model.enumeration.$value = new P21.Enumeration(false);
model.binary.$value = new P21.Binary("0F");
model.eid.$value = new P21.EID("21");
model.vid.$value = new P21.VID("31");
model.cin.$value = new P21.CIN("FOOT");
model.cvn.$value = new P21.CVN("TAU");
model["null-value"].$value = null;
model.list.$value = new P21.List(new P21.Integer(2), new P21.String("changed"), null);
model.resource.$value = new P21.URI("next.step#anchor");
model.tagged.$label = new P21.String("changed label");
model.set_uri(new P21.URI("https://example.test/changed.step"));
check("F.4.4", () => assert.equal(model.uri().toString(), "https://example.test/changed.step"));
model.set_name(new P21.String("annex-f.changed"));
check("F.4.5", () => assert.equal(model.name().valueOf(), "annex-f.changed"));

initialPopulation.set_uri(new P21.URI("https://example.test/changed-population.step"));
check(() => assert.equal(initialPopulation.verification(), false));
initialPopulation.set_stamp(new Date("2026-09-08T01:02:03.004Z"));
check(() => assert.equal(initialPopulation.verification(), false));
initialPopulation.set_verification();
check(() => assert.equal(initialPopulation.verification(), true));
const added = new P21.Population();
added.set_uri(new P21.URI("relative-population.step"));
added.set_stamp(new Date("2026-09-09T00:00:00.000Z"));
model.set_schema_population([initialPopulation, added]);
check("F.4.6", () => assert.equal(model.schema_population().length, 2));
model.schema_population()[1].set_verification();

check(() => assert.equal(model.integer.$value.toP21String(), "42"));
check(() => assert.equal(model.real.$value.toP21String(), "10."));
check(() => assert.equal(model.enumeration.$value.toP21String(), ".F."));
check(() => assert.equal(model.schema_population().length, 2));
check(() => assert.equal(model.schema_population()[1].verification(), true));

const collisionState = {
    formatVersion: 1,
    uri: "urn:collision",
    name: "collision.model",
    anchors: [
        { name: "uri", value: { kind: "integer", p21: "1" }, tags: [] },
        { name: "name", value: { kind: "integer", p21: "2" }, tags: [] },
        { name: "schema_population", value: { kind: "integer", p21: "3" }, tags: [] },
        { name: "set_uri", value: { kind: "integer", p21: "4" }, tags: [] },
        { name: "set_name", value: { kind: "integer", p21: "5" }, tags: [] },
        { name: "set_schema_population", value: { kind: "integer", p21: "6" }, tags: [] },
        { name: "_state", value: { kind: "string", value: "private-safe" }, tags: [] },
        { name: "tag-collision", value: { kind: "integer", p21: "2" }, tags: [
            { name: "value", value: { kind: "string", value: "tag-value" } }
        ] }
    ],
    schemaPopulation: [
        { uri: "urn:population", stamp: null, messageDigest: "AQID", verification: true }
    ]
};
let collisionLatest = collisionState;
const collisionModel = new P21.Model({
    snapshot: () => collisionLatest,
    apply: state => { collisionLatest = state; return collisionLatest; }
});
check(() => assert.equal(typeof collisionModel.uri, "function"));
check(() => assert.equal(collisionModel.uri().toString(), "urn:collision"));
check(() => assert.equal(collisionModel.uri.$value.valueOf(), 1));
check(() => assert.equal(collisionModel.name().valueOf(), "collision.model"));
check(() => assert.equal(collisionModel.name.$value.valueOf(), 2));
check(() => assert.equal(collisionModel.schema_population().length, 1));
check(() => assert.equal(collisionModel.schema_population.$value.valueOf(), 3));
collisionModel.set_uri(new P21.URI("urn:collision-changed"));
check(() => assert.equal(collisionModel.uri().toString(), "urn:collision-changed"));
check(() => assert.equal(collisionModel.set_uri.$value.valueOf(), 4));
collisionModel.set_name(new P21.String("collision.changed"));
check(() => assert.equal(collisionModel.name().valueOf(), "collision.changed"));
check(() => assert.equal(collisionModel.set_name.$value.valueOf(), 5));
collisionModel.set_schema_population(collisionModel.schema_population());
check(() => assert.equal(collisionModel.set_schema_population.$value.valueOf(), 6));
check(() => assert.equal(collisionModel._state.$value.valueOf(), "private-safe"));
check(() => assert.equal(collisionModel["tag-collision"].$value.anchorValue.valueOf(), 2));
check(() => assert.equal(collisionModel["tag-collision"].$value.tagValue.valueOf(), "tag-value"));
collisionModel["tag-collision"].$value.tagValue = new P21.String("changed-tag");
check(() => assert.equal(collisionModel["tag-collision"].$value.tagValue.valueOf(), "changed-tag"));

check(() => assert.throws(() => new P21.Model({ snapshot: () => collisionState }), /transactional apply/));
let rejectedState = collisionState;
const rejectedModel = new P21.Model({
    snapshot: () => rejectedState,
    apply: () => { throw new Error("rejected"); }
});
check(() => assert.throws(() => rejectedModel.set_name(new P21.String("after")), /rejected/));
check(() => assert.equal(rejectedModel.name().valueOf(), "collision.model"));
check(() => assert.throws(() => { rejectedModel.uri.$value = new P21.Integer(9); }, /rejected/));
check(() => assert.equal(rejectedModel.uri.$value.valueOf(), 1));
check(() => assert.throws(() => {
    rejectedModel["tag-collision"].$value.tagValue = new P21.String("after");
}, /rejected/));
check(() => assert.equal(rejectedModel["tag-collision"].$value.tagValue.valueOf(), "tag-value"));
check(() => assert.throws(() => {
    rejectedModel.schema_population()[0].set_uri(new P21.URI("urn:after"));
}, /rejected/));
check(() => assert.equal(rejectedModel.schema_population()[0].uri().toString(), "urn:population"));

const expectedRequirements = manifest.requirements.map(item => item.id).sort();
check(() => assert.deepEqual(Array.from(covered).sort(), expectedRequirements));

fs.writeFileSync(process.argv[4], JSON.stringify(latest), "utf8");
process.stdout.write("ANNEX_F_NODE_OK assertions=" + assertions + " requirements=" + covered.size + "\n");
