// Cobertura de código para el CI (requisito del profesor: comentario en cada PR y mínimo de 85 %).
//
// Uso:
//   node coverage.mjs check <backend|frontend> <resumen.json>
//   node coverage.mjs comment <Summary.json del backend> <coverage-summary.json del frontend> <salida.md>
//
// Variables de entorno:
//   COVERAGE_THRESHOLD  porcentaje mínimo de líneas (por defecto 85)
//   ENFORCE_COVERAGE    "true" hace fallar el job si no se alcanza el mínimo; con otro valor solo avisa
//   HEAD_SHA            commit que se informa en el comentario
import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';

export const MARKER = '<!-- trivia-coverage-report -->';

const threshold = Number(process.env.COVERAGE_THRESHOLD ?? 85);
const enforce = process.env.ENFORCE_COVERAGE === 'true';

const LABELS = { backend: 'Backend (.NET)', frontend: 'Frontend (Aurelia)' };

export function readSummary(kind, file) {
  if (!file || !existsSync(file)) return null;
  const json = JSON.parse(readFileSync(file, 'utf8'));
  if (kind === 'backend') {
    // JsonSummary de ReportGenerator
    const s = json.summary;
    return {
      lines: s.linecoverage,
      covered: s.coveredlines,
      total: s.coverablelines,
      branches: s.branchcoverage,
      parts: (json.coverage?.assemblies ?? []).map(a => ({ name: a.name, lines: a.coverage, branches: a.branchcoverage })),
    };
  }
  // json-summary de Jest
  const t = json.total;
  return { lines: t.lines.pct, covered: t.lines.covered, total: t.lines.total, branches: t.branches.pct, parts: [] };
}

const pct = value => (value == null ? '—' : `${value.toFixed(1).replace('.', ',')} %`);

function status(result) {
  if (!result) return '❔ Sin reporte';
  if (result.lines >= threshold) return '✅ Cumple';
  return enforce ? `❌ Bajo el ${threshold} %` : `⚠️ Bajo el ${threshold} %`;
}

export function buildComment(backend, frontend, sha = '') {
  const row = (kind, r) => r
    ? `| ${LABELS[kind]} | ${pct(r.lines)} (${r.covered}/${r.total}) | ${pct(r.branches)} | ${status(r)} |`
    : `| ${LABELS[kind]} | — | — | ${status(r)} |`;
  const lines = [
    MARKER,
    '## Cobertura de código',
    '',
    '| Parte | Líneas | Ramas | Estado |',
    '|---|---|---|---|',
    row('backend', backend),
    row('frontend', frontend),
    '',
  ];
  if (backend?.parts.length) {
    lines.push('<details><summary>Backend por proyecto</summary>', '', '| Proyecto | Líneas | Ramas |', '|---|---|---|');
    for (const part of backend.parts) lines.push(`| ${part.name} | ${pct(part.lines)} | ${pct(part.branches)} |`);
    lines.push('', '</details>', '');
  }
  lines.push(enforce
    ? `Mínimo requerido: **${threshold} %** de líneas. Si alguna parte no lo alcanza, el CI falla.`
    : `Mínimo requerido: **${threshold} %** de líneas. La validación está en modo aviso (\`ENFORCE_COVERAGE: "false"\`): todavía no hace fallar el CI.`);
  if (sha) lines.push('', `<sub>Commit ${sha.slice(0, 7)}</sub>`);
  return lines.join('\n') + '\n';
}

function check(kind, file) {
  const result = readSummary(kind, file);
  if (!result) {
    console.log(`::error title=Cobertura ${kind}::No se encontró el reporte ${file}`);
    process.exit(1);
  }
  const message = `${LABELS[kind]}: ${pct(result.lines)} de líneas (${result.covered}/${result.total}), mínimo ${threshold} %`;
  console.log(message);
  if (process.env.GITHUB_STEP_SUMMARY) {
    appendFileSync(process.env.GITHUB_STEP_SUMMARY, `### Cobertura: ${LABELS[kind]}\n\n${status(result)} · ${message}\n\n`);
  }
  if (result.lines < threshold) {
    if (enforce) {
      console.log(`::error title=Cobertura insuficiente::${message}`);
      process.exit(1);
    }
    console.log(`::warning title=Cobertura insuficiente::${message} (modo aviso, no falla el CI)`);
  }
}

const [command, ...args] = process.argv.slice(2);
if (command === 'check') {
  check(args[0], args[1]);
} else if (command === 'comment') {
  const [backendFile, frontendFile, output] = args;
  writeFileSync(output, buildComment(readSummary('backend', backendFile), readSummary('frontend', frontendFile), process.env.HEAD_SHA ?? ''));
  console.log(`Comentario escrito en ${output}`);
} else if (command) {
  console.error(`Comando desconocido: ${command}`);
  process.exit(2);
}
