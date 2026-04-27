import { Card } from './Card';
import { Skeleton, SkeletonText } from './Skeleton';

/** Editorial page header that mirrors `<PageHeader>` proportions. */
const HeaderSkeleton = ({ withEyebrow = false }: { withEyebrow?: boolean }) => (
  <div className="mb-10">
    {withEyebrow && <Skeleton className="mb-3 h-3 w-40" />}
    <Skeleton className="h-10 w-2/3 max-w-md" />
    <Skeleton className="mt-3 h-4 w-1/2 max-w-sm" />
  </div>
);

const RowSkeleton = ({ height = 'h-16' }: { height?: string }) => (
  <Card className={`flex items-center gap-4 px-5 ${height}`}>
    <Skeleton className="h-4 w-4 shrink-0 rounded-full" />
    <div className="flex-1 space-y-2">
      <Skeleton className="h-3.5 w-1/3" />
      <Skeleton className="h-3 w-1/4" />
    </div>
    <Skeleton className="h-3 w-16" />
  </Card>
);

/** Skeleton that mirrors a typical list page (Resumes, Vacancies, Sessions, Matches). */
export const ListPageSkeleton = ({ rows = 4 }: { rows?: number }) => (
  <>
    <HeaderSkeleton />
    <div className="space-y-2">
      {Array.from({ length: rows }).map((_, i) => (
        <RowSkeleton key={i} />
      ))}
    </div>
  </>
);

/** Skeleton that mirrors the Dashboard page layout. */
export const DashboardSkeleton = () => (
  <>
    <HeaderSkeleton withEyebrow />
    <Card className="p-7">
      <div className="flex items-start gap-6">
        <Skeleton className="h-24 w-24 shrink-0 rounded-full" />
        <div className="flex-1 space-y-3">
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-5 w-3/4 max-w-md" />
          <SkeletonText lines={2} />
        </div>
      </div>
    </Card>
    <div className="mt-8 grid gap-3 sm:grid-cols-2">
      <RowSkeleton />
      <RowSkeleton />
      <RowSkeleton />
      <RowSkeleton />
    </div>
  </>
);

/** Skeleton that mirrors the Match detail page layout. */
export const MatchDetailSkeleton = () => (
  <>
    <HeaderSkeleton />
    <div className="mb-6 flex gap-2">
      <Skeleton className="h-7 w-40 rounded-full" />
      <Skeleton className="h-7 w-56 rounded-full" />
    </div>
    <div className="grid gap-6 md:grid-cols-[auto_1fr]">
      <Card className="flex items-center justify-center p-8">
        <Skeleton className="h-28 w-28 rounded-full" />
      </Card>
      <Card className="space-y-4 p-7">
        <Skeleton className="h-3.5 w-full" />
        <Skeleton className="h-3.5 w-full" />
        <Skeleton className="h-3.5 w-full" />
      </Card>
    </div>
    <Card className="mt-6 p-5">
      <SkeletonText lines={3} />
    </Card>
    <div className="mt-8 space-y-3">
      {Array.from({ length: 3 }).map((_, i) => (
        <Card key={i} className="p-5">
          <Skeleton className="mb-3 h-4 w-1/2" />
          <SkeletonText lines={2} />
        </Card>
      ))}
    </div>
  </>
);

/** Skeleton that mirrors the Session detail page layout. */
export const SessionDetailSkeleton = () => (
  <>
    <HeaderSkeleton withEyebrow />
    <div className="mb-8 flex gap-2">
      <Skeleton className="h-7 w-44 rounded-full" />
      <Skeleton className="h-7 w-56 rounded-full" />
    </div>
    <div className="space-y-6">
      <Skeleton className="h-3 w-32" />
      <Skeleton className="h-7 w-3/4" />
      <Skeleton className="h-32 w-full rounded-xl" />
    </div>
  </>
);
