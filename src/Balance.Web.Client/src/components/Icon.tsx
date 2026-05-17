import {
    ArrowLeftRight,
    Banknote,
    Bell,
    BookOpen,
    ChevronRight,
    CircleHelp,
    Cloud,
    Coffee,
    CreditCard,
    Download,
    Home,
    LayoutDashboard,
    LineChart,
    Music,
    Palette,
    PiggyBank,
    Plus,
    Repeat,
    Search,
    Settings,
    ShoppingCart,
    Train,
    TrendingDown,
    TrendingUp,
    Tv,
    Wallet,
    Zap,
    type LucideIcon,
    type LucideProps,
} from 'lucide-react';

/*
 * Icon registry. Demo data references icons by kebab-case names (so a
 * future API can carry them as strings); the component maps each name to a
 * tree-shaken lucide-react import. Add new icons here as screens grow.
 */
const REGISTRY: Record<string, LucideIcon> = {
    'arrow-left-right': ArrowLeftRight,
    'banknote': Banknote,
    'bell': Bell,
    'book-open': BookOpen,
    'chevron-right': ChevronRight,
    'cloud': Cloud,
    'coffee': Coffee,
    'credit-card': CreditCard,
    'download': Download,
    'home': Home,
    'layout-dashboard': LayoutDashboard,
    'line-chart': LineChart,
    'music': Music,
    'palette': Palette,
    'piggy-bank': PiggyBank,
    'plus': Plus,
    'repeat': Repeat,
    'search': Search,
    'settings': Settings,
    'shopping-cart': ShoppingCart,
    'train': Train,
    'trending-down': TrendingDown,
    'trending-up': TrendingUp,
    'tv': Tv,
    'wallet': Wallet,
    'zap': Zap,
};

export type IconProps = LucideProps & { name: string };

export function Icon({ name, ...rest }: IconProps) {
    const Component = REGISTRY[name] ?? CircleHelp;
    return <Component {...rest} />;
}
