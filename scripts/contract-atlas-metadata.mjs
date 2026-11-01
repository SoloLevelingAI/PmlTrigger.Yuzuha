import { build } from './lib/contract-atlas-metadata.mjs';
const command = process.argv[2];
if (!['build', 'check'].includes(command)) { process.stderr.write('Usage: node scripts/contract-atlas-metadata.mjs build|check\n'); process.exitCode = 2; }
else { try { process.stdout.write(JSON.stringify(build(undefined, command === 'check')) + '\n'); } catch (error) { process.stderr.write(error.message + '\n'); process.exitCode = 1; } }
