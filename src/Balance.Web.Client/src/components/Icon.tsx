import {
    AlertCircle,
    ArrowLeftRight,
    Banknote,
    Bell,
    BookOpen,
    CheckCircle2,
    ChevronRight,
    CircleHelp,
    Cloud,
    Coffee,
    CreditCard,
    Download,
    Home,
    Info,
    Landmark,
    LayoutDashboard,
    LineChart,
    MoreVertical,
    Music,
    Palette,
    Pencil,
    PiggyBank,
    Plus,
    Repeat,
    Search,
    Settings,
    ShoppingCart,
    Train,
    Trash2,
    TrendingDown,
    TrendingUp,
    Tv,
    Wallet,
    X,
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
    'alert-circle': AlertCircle,
    'arrow-left-right': ArrowLeftRight,
    banknote: Banknote,
    bell: Bell,
    'book-open': BookOpen,
    'check-circle': CheckCircle2,
    'chevron-right': ChevronRight,
    cloud: Cloud,
    coffee: Coffee,
    'credit-card': CreditCard,
    download: Download,
    home: Home,
    info: Info,
    landmark: Landmark,
    'layout-dashboard': LayoutDashboard,
    'line-chart': LineChart,
    'more-vertical': MoreVertical,
    music: Music,
    palette: Palette,
    pencil: Pencil,
    'piggy-bank': PiggyBank,
    plus: Plus,
    repeat: Repeat,
    search: Search,
    settings: Settings,
    'shopping-cart': ShoppingCart,
    train: Train,
    trash: Trash2,
    'trending-down': TrendingDown,
    'trending-up': TrendingUp,
    tv: Tv,
    wallet: Wallet,
    x: X,
    zap: Zap,
};

export type IconProps = LucideProps & { name: string };

export function Icon({ name, ...rest }: IconProps) {
    const Component = REGISTRY[name] ?? CircleHelp;
    return <Component {...rest} />;
}
