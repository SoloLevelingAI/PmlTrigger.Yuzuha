import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));
const { projects } = JSON.parse(await readFile(path.join(root, 'docs/architecture/dependencies.json'), 'utf8'));
const lines = [
  '// Generated from dependencies.json by npm run architecture:generate. Do not edit.',
  'specification {', '  element system', '  element component', '  element actor', '  element store', '  relationship build', '}',
  'model {', '  yuzuha = system "PmlTrigger.Yuzuha" {'
];
for (const [name, project] of Object.entries(projects))
  lines.push(`    ${project.id} = component ${JSON.stringify(project.title)} { description ${JSON.stringify(name)} }`);
lines.push('  }');
for (const project of Object.values(projects)) {
  for (const target of project.references) {
    if (!projects[target]) throw new Error(`Unknown project ${target}`);
    lines.push(`  yuzuha.${project.id} -[build]-> yuzuha.${projects[target].id} "ProjectReference"`);
  }
}
lines.push('}', 'views {', '  view project_dependencies {', '    title "编译依赖 · 项目引用白名单"', '    include yuzuha.*', '    exclude * -> * where kind is not build', '    autoLayout LeftRight', '  }', '}', '');
const generated = lines.join('\n');
const destination = path.join(root, 'docs/architecture/projects.c4');
const mode = process.argv[2] ?? 'check';
if (mode === 'generate') {
  await writeFile(destination, generated);
  console.log('Generated docs/architecture/projects.c4');
} else if (mode === 'check') {
  if (await readFile(destination, 'utf8') !== generated)
    throw new Error('Architecture projection is stale. Run npm run architecture:generate and review the diff.');
  console.log('Architecture projection matches the approved dependency graph.');
} else throw new Error(`Unknown command: ${mode}`);
