import { Card } from "@/components/ui";
import { adminGet } from "@/lib/admin";

interface Report {
  totalDocuments: number; unclassified: number; classified: number; verified: number; awaitingVotes: number;
  awaitingReview: number; rejected: number; tasksAssigned: number; tasksCompleted: number; taskCompletionRate: number;
  totalVotes: number; resolvedVotes: number; votingAgreementRate: number; validContributions: number;
  skippedAssignments: number; proposalsPending: number; proposalsApproved: number; proposalsRejected: number;
  proposalsMerged: number; totalDownloads: number; activeContributors: number; usersOverFreeQuota: number;
}

function Stat({ label, value, hint }: { label: string; value: string | number; hint?: string }) {
  return (
    <Card className="p-4">
      <p className="text-xs uppercase tracking-wide text-ink-soft">{label}</p>
      <p className="mt-1 text-2xl font-semibold">{value}</p>
      {hint ? <p className="mt-1 text-xs text-ink-soft">{hint}</p> : null}
    </Card>
  );
}

export default async function ClassificationReportPage() {
  const r = await adminGet<Report>("/api/admin/classification/report");
  return (
    <div className="space-y-8">
      <h1 className="text-2xl font-semibold">Rapports de classification</h1>

      <section>
        <h2 className="mb-3 font-semibold">Documents</h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Stat label="Documents" value={r.totalDocuments} />
          <Stat label="Classés" value={r.classified} />
          <Stat label="Vérifiés" value={r.verified} />
          <Stat label="À classer" value={r.unclassified} hint="Non classés (inclut ceux en cours de vote)" />
          <Stat label="En attente de votes" value={r.awaitingVotes} />
          <Stat label="En attente de revue" value={r.awaitingReview} />
          <Stat label="Rejetés" value={r.rejected} />
        </div>
      </section>

      <section>
        <h2 className="mb-3 font-semibold">Participation</h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Stat label="Tâches attribuées" value={r.tasksAssigned} />
          <Stat label="Taux de complétion" value={`${r.taskCompletionRate} %`} hint="Terminées / (terminées + expirées)" />
          <Stat label="Contributions valides" value={r.validContributions} />
          <Stat label="Contributeurs actifs" value={r.activeContributors} />
          <Stat label="Votes" value={r.totalVotes} />
          <Stat label="Accord avec le résultat" value={`${r.votingAgreementRate} %`} hint={`${r.resolvedVotes} votes résolus`} />
          <Stat label="Documents passés" value={r.skippedAssignments} />
        </div>
      </section>

      <section>
        <h2 className="mb-3 font-semibold">Valeurs proposées</h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Stat label="En attente" value={r.proposalsPending} />
          <Stat label="Approuvées" value={r.proposalsApproved} />
          <Stat label="Fusionnées" value={r.proposalsMerged} />
          <Stat label="Rejetées" value={r.proposalsRejected} />
        </div>
      </section>

      <section>
        <h2 className="mb-3 font-semibold">Téléchargements et accès</h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Stat label="Téléchargements suivis" value={r.totalDownloads} hint="Depuis l'activation du suivi" />
          <Stat label="Quota atteint" value={r.usersOverFreeQuota} hint="Utilisateurs ayant épuisé leur quota (si activé)" />
        </div>
      </section>
    </div>
  );
}
