import { cx } from '../lib/cx';

type SkeletonProps = {
    className?: string;
};

export function Skeleton({ className }: SkeletonProps) {
    return (
        <div
            className={cx('rounded-sm bg-surface-2 animate-pulse', className)}
            aria-hidden="true"
        />
    );
}
