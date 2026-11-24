const fs = require("node:fs");
const path = require("node:path");

if (!process.argv[2]) throw new Error("Pass the path to Core30's design-tokens.json.");
const source = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const lines = ["; Generated from Core30 design-tokens.json."];

function value(group, name) {
  const token = source[group]?.[name]?.$value;
  if (token === undefined) throw new Error(`Missing Core30 token: ${group}.${name}`);
  const alias = typeof token === "string" && token.match(/^\{([^.]+)\.([^}]+)\}$/);
  return alias ? value(alias[1], alias[2]) : token;
}

function define(name, data) {
  lines.push("#define Core30" + name + " " + (typeof data === "number" ? data : JSON.stringify(data)));
}

const colors = {
  Canvas: "canvas", Surface: "surface", Sidebar: "sidebar", Selected: "selected",
  Hero: "hero", HeroEdge: "hero-edge", Border: "border", BorderStrong: "border-strong", Ink: "text", Muted: "muted",
  Primary: "primary", PrimaryHover: "primary-hover", OnPrimary: "on-primary", Success: "success", SuccessBg: "success-bg",
  Danger: "danger", DangerBg: "danger-bg"
};
for (const [name, token] of Object.entries(colors)) {
  const color = value("color", token);
  const channels = color.hex.slice(1).match(/../g);
  define(name, "$" + channels.reverse().join(""));
  if (name === "Canvas") define("CanvasHex", color.hex);
}

for (const [name, token] of Object.entries({ Gap: "sm", Inset: "lg", CardInset: "2xl" }))
  define(name, value("space", token).value);
for (const [name, token] of Object.entries({ ControlRadius: "control", CardRadius: "card" }))
  define(name, value("radius", token).value);
for (const [name, token] of Object.entries({ LabelPt: "label", SectionPt: "section", TitlePt: "title" }))
  define(name, Math.round(value("font-size", token).value * 12));

const family = value("font-family", "ui").find(name => name === "Microsoft YaHei UI");
if (!family) throw new Error("The Core30 Chinese UI font is missing.");
define("ChineseFont", family);
fs.writeFileSync(path.join(__dirname, "Core30Light.Tokens.iss"), lines.join("\n") + "\n");
