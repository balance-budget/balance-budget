import type { ReactNode } from 'react';
import {
    Tag as AriaTag,
    TagGroup as AriaTagGroup,
    type TagGroupProps as AriaTagGroupProps,
    TagList,
    type TagListProps,
    type TagProps,
} from 'react-aria-components';
import { cx } from '../../lib/cx';
import { composeTailwindRenderProps } from './compose';
import { Label } from './field';

export type TagGroupProps<T extends object> = Omit<AriaTagGroupProps, 'children'> &
    Pick<TagListProps<T>, 'items' | 'children'> & {
        label?: string;
    };

/** Pill-shaped selectable tags — the Balance "filter chips" look. */
export function TagGroup<T extends object>({ label, items, children, ...props }: TagGroupProps<T>) {
    return (
        <AriaTagGroup {...props} className={cx('flex items-center gap-2', props.className)}>
            {label !== undefined && <Label>{label}</Label>}
            <TagList items={items} className="flex flex-wrap items-center gap-[6px]">
                {children as ReactNode | ((item: T) => ReactNode)}
            </TagList>
        </AriaTagGroup>
    );
}

export function Tag(props: TagProps) {
    return (
        <AriaTag
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'px-[10px] py-[5px] rounded-full text-11 font-medium select-none cursor-pointer outline-none ' +
                    'text-fg-3 data-[hovered]:text-fg-1 ' +
                    'data-[selected]:bg-brand-primary-soft data-[selected]:text-brand-primary ' +
                    'data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary ' +
                    'data-[disabled]:opacity-60',
            )}
        />
    );
}
