using MudBlazor;

namespace BebesNadadores.Web.Themes;

public static class BebesNadadoresTheme
{
    private static readonly string[] FuenteTitulo =
    [
        "Montserrat",
        "Segoe UI",
        "Arial",
        "sans-serif"
    ];

    private static readonly string[] FuenteTexto =
    [
        "Lora",
        "Georgia",
        "serif"
    ];


    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#078BD3",
            Secondary = "#27C2F3",

            Background = "#F4F9FC",
            Surface = "#FFFFFF",

            AppbarBackground = "#FFFFFF",
            AppbarText = "#0B2B45",

            DrawerBackground = "#064B78",
            DrawerText = "#FFFFFF",
            DrawerIcon = "#DDF5FF",

            TextPrimary = "#102C42",
            TextSecondary = "#6B8191",

            ActionDefault = "#4D6F85",

            Divider = "#DCE8EF",

            Success = "#25C67A",
            Warning = "#F4B740",
            Error = "#ED5264",
            Info = "#24A9E8"
        },


        PaletteDark = new PaletteDark
        {
            Primary = "#21B6F3",
            Secondary = "#56D5FF",

            Background = "#061B2C",
            Surface = "#0B263B",

            AppbarBackground = "#071F32",
            AppbarText = "#F3FAFE",

            DrawerBackground = "#052844",
            DrawerText = "#F1F9FD",
            DrawerIcon = "#7DDCFF",

            TextPrimary = "#F4FAFD",
            TextSecondary = "#91AABC",

            ActionDefault = "#8AB4CA",

            Divider = "#173E57",

            Success = "#35D98A",
            Warning = "#FFBE45",
            Error = "#FF5A68",
            Info = "#32C5FF"
        },


        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "14px"
        },


        Typography = new Typography
        {
            // =================================================
            // TEXTO GENERAL
            // =================================================

            Default = new DefaultTypography
            {
                FontFamily = FuenteTexto,

                FontSize = "0.95rem",

                FontWeight = "400",

                LineHeight = "1.55"
            },


            Body1 = new Body1Typography
            {
                FontFamily = FuenteTexto,

                FontSize = "0.95rem",

                FontWeight = "400",

                LineHeight = "1.55"
            },


            Body2 = new Body2Typography
            {
                FontFamily = FuenteTexto,

                FontSize = "0.85rem",

                FontWeight = "400",

                LineHeight = "1.5"
            },


            // =================================================
            // TITULOS
            // =================================================

            H1 = new H1Typography
            {
                FontFamily = FuenteTitulo,

                FontSize = "2rem",

                FontWeight = "800",

                LineHeight = "1.2"
            },


            H2 = new H2Typography
            {
                FontFamily = FuenteTitulo,

                FontSize = "1.65rem",

                FontWeight = "800",

                LineHeight = "1.25"
            },


            H3 = new H3Typography
            {
                FontFamily = FuenteTitulo,

                FontSize = "1.35rem",

                FontWeight = "700",

                LineHeight = "1.3"
            },


            H4 = new H4Typography
            {
                FontFamily = FuenteTitulo,

                FontSize = "1.15rem",

                FontWeight = "700",

                LineHeight = "1.35"
            },


            H5 = new H5Typography
            {
                FontFamily = FuenteTitulo,

                FontSize = "1rem",

                FontWeight = "700"
            },


            H6 = new H6Typography
            {
                FontFamily = FuenteTitulo,

                FontSize = "0.95rem",

                FontWeight = "700"
            },


            // =================================================
            // UI
            // =================================================

            Button = new ButtonTypography
            {
                FontFamily = FuenteTitulo,

                FontSize = "0.78rem",

                FontWeight = "700",

                LetterSpacing = "0"
            },


            Caption = new CaptionTypography
            {
                FontFamily = FuenteTitulo,

                FontSize = "0.72rem",

                FontWeight = "500"
            },


            Subtitle1 = new Subtitle1Typography
            {
                FontFamily = FuenteTitulo,

                FontSize = "0.95rem",

                FontWeight = "600"
            },


            Subtitle2 = new Subtitle2Typography
            {
                FontFamily = FuenteTitulo,

                FontSize = "0.82rem",

                FontWeight = "600"
            }
        }
    };
}