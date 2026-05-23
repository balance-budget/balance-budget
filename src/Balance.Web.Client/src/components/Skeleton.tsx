type SkeletonProps = {
    className?: string;
};

export function Skeleton({ className }: SkeletonProps) {
    return (
        <span
            className={['inline-block rounded-sm bg-surface-2 animate-pulse', className]
                .filter(Boolean)
                .join(' ')}
            aria-hidden="true"
        />
    );
}
