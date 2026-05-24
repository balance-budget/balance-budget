import { Icon } from './Icon';
import { cx } from '../lib/cx';

type PaginationProps = {
    page: number;
    pageSize: number;
    /** Number of items on the current page. The list endpoint doesn't return a
     *  total count, so "has next" is inferred from `count === pageSize`. */
    count: number;
    onPageChange: (page: number) => void;
};

export function Pagination({ page, pageSize, count, onPageChange }: PaginationProps) {
    const hasPrev = page > 1;
    const hasNext = count === pageSize;
    const from = (page - 1) * pageSize + 1;
    const to = (page - 1) * pageSize + count;

    return (
        <div className="flex items-center justify-between gap-3 pt-3">
            <span className="text-[12px] text-fg-3 tabular">
                {count === 0 ? '—' : `${from}–${to}`}
            </span>
            <div className="flex items-center gap-1">
                <PageButton
                    label="Previous page"
                    icon="chevron-right"
                    rotated
                    disabled={!hasPrev}
                    onClick={() => {
                        onPageChange(page - 1);
                    }}
                />
                <span className="px-2 text-[12px] text-fg-2 tabular">Page {page}</span>
                <PageButton
                    label="Next page"
                    icon="chevron-right"
                    disabled={!hasNext}
                    onClick={() => {
                        onPageChange(page + 1);
                    }}
                />
            </div>
        </div>
    );
}

function PageButton({
    label,
    icon,
    rotated,
    disabled,
    onClick,
}: {
    label: string;
    icon: string;
    rotated?: boolean;
    disabled: boolean;
    onClick: () => void;
}) {
    return (
        <button
            type="button"
            aria-label={label}
            disabled={disabled}
            onClick={onClick}
            className={cx(
                'p-2 rounded-sm text-fg-3 hover:text-fg-1 hover:bg-surface-2',
                disabled && 'opacity-30 hover:bg-transparent hover:text-fg-3 cursor-not-allowed',
            )}
        >
            <Icon
                name={icon}
                size={14}
                strokeWidth={2}
                className={rotated ? 'rotate-180' : undefined}
            />
        </button>
    );
}
