"use client";

import { useActionState, useEffect, useState } from "react";
import { Button } from "@/components/ui";

type Action = (prev: { error?: string }, formData: FormData) => Promise<{ error?: string }>;

interface Node {
  id: string;
  name: string;
}

interface InitialProfile {
  firstName: string;
  lastName: string;
  studentId: string | null;
  universityId: string | null;
  facultyId: string | null;
  departmentId: string | null;
  specialtyId: string | null;
  levelId: string | null;
}

async function fetchList(resource: string, parentId?: string): Promise<Node[]> {
  const qs = new URLSearchParams({ pageSize: "200" });
  if (parentId) qs.set("parentId", parentId);
  const res = await fetch(`/api/${resource}?${qs.toString()}`, { cache: "no-store" });
  if (!res.ok) return [];
  const body = (await res.json()) as { data: Node[] };
  return body.data ?? [];
}

function Select({
  label, value, onChange, options, disabled, placeholder,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  options: Node[];
  disabled?: boolean;
  placeholder: string;
}) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block font-medium text-ink-muted">{label}</span>
      <select
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-md border border-line bg-paper-raised px-3 py-2 disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-brand"
      >
        <option value="">{placeholder}</option>
        {options.map((o) => (
          <option key={o.id} value={o.id}>{o.name}</option>
        ))}
      </select>
    </label>
  );
}

export function ProfileForm({ action, initial }: { action: Action; initial: InitialProfile }) {
  const [state, formAction, pending] = useActionState(action, {});

  const [universities, setUniversities] = useState<Node[]>([]);
  const [faculties, setFaculties] = useState<Node[]>([]);
  const [departments, setDepartments] = useState<Node[]>([]);
  const [specialties, setSpecialties] = useState<Node[]>([]);
  const [levels, setLevels] = useState<Node[]>([]);

  const [universityId, setUniversityId] = useState(initial.universityId ?? "");
  const [facultyId, setFacultyId] = useState(initial.facultyId ?? "");
  const [departmentId, setDepartmentId] = useState(initial.departmentId ?? "");
  const [specialtyId, setSpecialtyId] = useState(initial.specialtyId ?? "");
  const [levelId, setLevelId] = useState(initial.levelId ?? "");

  useEffect(() => {
    void fetchList("universities").then(setUniversities);
  }, []);

  useEffect(() => {
    if (!universityId) { setFaculties([]); return; }
    void fetchList("faculties", universityId).then(setFaculties);
  }, [universityId]);

  useEffect(() => {
    if (!facultyId) { setDepartments([]); return; }
    void fetchList("departments", facultyId).then(setDepartments);
  }, [facultyId]);

  useEffect(() => {
    if (!departmentId) { setSpecialties([]); return; }
    void fetchList("specialties", departmentId).then(setSpecialties);
  }, [departmentId]);

  useEffect(() => {
    if (!specialtyId) { setLevels([]); return; }
    void fetchList("levels", specialtyId).then(setLevels);
  }, [specialtyId]);

  return (
    <form action={formAction} className="space-y-4">
      <input type="hidden" name="universityId" value={universityId} />
      <input type="hidden" name="facultyId" value={facultyId} />
      <input type="hidden" name="departmentId" value={departmentId} />
      <input type="hidden" name="specialtyId" value={specialtyId} />
      <input type="hidden" name="levelId" value={levelId} />

      <div className="grid gap-4 sm:grid-cols-2">
        <label className="block text-sm">
          <span className="mb-1 block font-medium text-ink-muted">Prénom</span>
          <input name="firstName" defaultValue={initial.firstName} required className="w-full rounded-md border border-line bg-paper-raised px-3 py-2" />
        </label>
        <label className="block text-sm">
          <span className="mb-1 block font-medium text-ink-muted">Nom</span>
          <input name="lastName" defaultValue={initial.lastName} required className="w-full rounded-md border border-line bg-paper-raised px-3 py-2" />
        </label>
      </div>

      <label className="block text-sm">
        <span className="mb-1 block font-medium text-ink-muted">Matricule (optionnel)</span>
        <input name="studentId" defaultValue={initial.studentId ?? ""} className="w-full rounded-md border border-line bg-paper-raised px-3 py-2" />
      </label>

      <Select label="Université" placeholder="Choisir…" value={universityId} options={universities}
        onChange={(v) => { setUniversityId(v); setFacultyId(""); setDepartmentId(""); setSpecialtyId(""); setLevelId(""); }} />
      <Select label="Faculté" placeholder="Choisir…" value={facultyId} options={faculties} disabled={!universityId}
        onChange={(v) => { setFacultyId(v); setDepartmentId(""); setSpecialtyId(""); setLevelId(""); }} />
      <Select label="Département" placeholder="Choisir…" value={departmentId} options={departments} disabled={!facultyId}
        onChange={(v) => { setDepartmentId(v); setSpecialtyId(""); setLevelId(""); }} />
      <Select label="Spécialité" placeholder="Choisir…" value={specialtyId} options={specialties} disabled={!departmentId}
        onChange={(v) => { setSpecialtyId(v); setLevelId(""); }} />
      <Select label="Niveau" placeholder="Choisir…" value={levelId} options={levels} disabled={!specialtyId}
        onChange={setLevelId} />

      {state.error ? <p className="text-sm text-red-700">{state.error}</p> : null}
      <Button type="submit" disabled={pending} className="w-full">
        {pending ? "Enregistrement…" : "Enregistrer mon profil"}
      </Button>
    </form>
  );
}
