import type {
  FindingSeverity,
  MatchReportResponse,
  ResumeResponse,
  VacancyResponse,
} from '@/types/api';

interface SerializeOptions {
  /** Localised section labels — passed in so the export language matches the UI. */
  labels: {
    matchReport: string;
    resume: string;
    vacancy: string;
    overall: string;
    skillCoverage: string;
    semanticSimilarity: string;
    experienceFit: string;
    improvementSummary: string;
    matchedMustHaves: string;
    missingMustHaves: string;
    matchedNiceToHaves: string;
    findings: string;
    recommendation: string;
    none: string;
  };
}

/**
 * Render a match report as portable Markdown — useful for pasting into a doc,
 * email, or a notes app. Order mirrors the on-screen layout so it reads like the same report.
 */
export function matchReportToMarkdown(
  match: MatchReportResponse,
  resume: ResumeResponse | undefined,
  vacancy: VacancyResponse | undefined,
  opts: SerializeOptions,
): string {
  const { labels } = opts;
  const lines: string[] = [];

  lines.push(`# ${labels.matchReport}`);
  lines.push('');
  lines.push(`*${new Date(match.createdAt).toLocaleString()}*`);
  if (resume?.fileName) lines.push(`- **${labels.resume}:** ${resume.fileName}`);
  if (vacancy?.title) {
    const v = vacancy.company ? `${vacancy.title} · ${vacancy.company}` : vacancy.title;
    lines.push(`- **${labels.vacancy}:** ${v}`);
  }
  lines.push('');

  lines.push('## Scores');
  lines.push('');
  lines.push(`| Metric | Score |`);
  lines.push(`| --- | ---: |`);
  lines.push(`| ${labels.overall} | **${Math.round(match.overallScore)} / 100** |`);
  lines.push(`| ${labels.skillCoverage} | ${Math.round(match.skillCoverageScore)} / 100 |`);
  lines.push(`| ${labels.semanticSimilarity} | ${Math.round(match.semanticSimilarityScore)} / 100 |`);
  lines.push(`| ${labels.experienceFit} | ${Math.round(match.experienceFitScore)} / 100 |`);
  lines.push('');

  if (match.improvementSummary) {
    lines.push(`## ${labels.improvementSummary}`);
    lines.push('');
    lines.push(match.improvementSummary.trim());
    lines.push('');
  }

  lines.push('## Skills');
  lines.push('');
  lines.push(`**${labels.matchedMustHaves}:** ${formatSkills(match.matchedMustHaveSkills, labels.none)}`);
  lines.push('');
  lines.push(`**${labels.missingMustHaves}:** ${formatSkills(match.missingMustHaveSkills, labels.none)}`);
  lines.push('');
  lines.push(`**${labels.matchedNiceToHaves}:** ${formatSkills(match.matchedNiceToHaveSkills, labels.none)}`);
  lines.push('');

  const findings = [...(match.findings ?? [])].sort(
    (a, b) => severityRank(a.severity) - severityRank(b.severity),
  );
  if (findings.length > 0) {
    lines.push(`## ${labels.findings}`);
    lines.push('');
    for (const f of findings) {
      lines.push(`### ${f.title}`);
      lines.push(`*${f.severity} · ${f.category}*`);
      lines.push('');
      lines.push(f.description);
      if (f.recommendation) {
        lines.push('');
        lines.push(`**${labels.recommendation}:** ${f.recommendation}`);
      }
      if (f.resumeExcerpt) {
        lines.push('');
        lines.push(`> ${f.resumeExcerpt}`);
      }
      lines.push('');
    }
  }

  return lines.join('\n').replace(/\n{3,}/g, '\n\n').trimEnd() + '\n';
}

const formatSkills = (skills: string[] | null | undefined, noneLabel: string) => {
  const list = (skills ?? []).filter(Boolean);
  if (list.length === 0) return `_${noneLabel}_`;
  return list.map((s) => `\`${s}\``).join(', ');
};

const severityRank = (s: FindingSeverity) =>
  s === 'Critical' ? 0 : s === 'Warning' ? 1 : 2;
