using System;
using System.Collections.Generic;
using System.Linq;

namespace Logistikum.Sim
{
    /// <summary>Ein Ziel für eine Ware samt freiem Platz (int.MaxValue bei der Export-Ausfahrt).</summary>
    public struct Drop
    {
        public Site Site;
        public int Free;
    }

    public sealed class PlannedJob
    {
        public Job Job;
        /// <summary>Zufahrt der Quelle.</summary>
        public Cell Target;
    }

    /// <summary>Automatik der LKW: Rangfolge der Fahrten und Ziele je Ware.</summary>
    public static class TruckJobs
    {
        /// <summary>Wohin eine Ware gebracht werden kann, in Reihenfolge der Vorliebe.</summary>
        public static List<Drop> DropsFor(GameState state, ProductId product, bool fromHall = false, List<Site> sites = null, Stock.Reservations res = null)
        {
            var all = sites ?? Sites.All(state);
            res = res ?? new Stock.Reservations(state);
            List<Drop> ToKind(SiteKind kind) => all
                .Where(s => s.Kind == kind)
                .Select(s => new Drop { Site = s, Free = res.FreeSpace(s.Id, product) })
                .Where(d => d.Free > 0).ToList();
            List<Drop> Exits() => Export.Price(product) == null
                ? new List<Drop>()
                : all.Where(s => s.Kind == SiteKind.export).Select(s => new Drop { Site = s, Free = int.MaxValue }).ToList();
            switch (product)
            {
                case ProductId.rawA: return ToKind(SiteKind.B);
                case ProductId.combo:
                {
                    // Lieber zur Weiterverarbeitung nach C; ist dort kein Platz, direkt in den Export.
                    var toC = ToKind(SiteKind.C);
                    return toC.Count > 0 ? toC : Exits();
                }
                case ProductId.final:
                {
                    // M3: Endprodukt lieber in den Wareneingang einer Halle, sonst in den Export.
                    if (fromHall) return Exits();
                    var toHall = ToKind(SiteKind.hall);
                    return toHall.Count > 0 ? toHall : Exits();
                }
                case ProductId.rawB: return new List<Drop>();
                default: return Exits();
            }
        }

        struct Priority { public ProductId Product; public SiteKind From; }

        /// <summary>
        /// Rangfolge (Entscheidung 08.10.2026, M3 ergänzt): fertige Hallenware zuerst, dann Endprodukt,
        /// dann Kombi, dann Rohware A.
        /// </summary>
        static readonly Priority[] Order =
        {
            new Priority { Product = ProductId.@checked, From = SiteKind.hall },
            new Priority { Product = ProductId.labeled, From = SiteKind.hall },
            new Priority { Product = ProductId.packed, From = SiteKind.hall },
            new Priority { Product = ProductId.final, From = SiteKind.hall },
            new Priority { Product = ProductId.final, From = SiteKind.C },
            new Priority { Product = ProductId.combo, From = SiteKind.B },
            new Priority { Product = ProductId.rawA, From = SiteKind.A },
        };

        /// <summary>Sucht die dringendste machbare Fahrt. null = nichts zu tun; noRoute = nur unerreichbar.</summary>
        public static PlannedJob Find(GameState state, RoadNetwork network, Cell position, int capacity, out bool noRoute,
            RouteCache routes = null, List<Site> sites = null)
        {
            noRoute = false;
            routes = routes ?? new RouteCache(network);
            var all = sites ?? Sites.All(state);
            var res = new Stock.Reservations(state);
            foreach (var pr in Order)
            {
                var sources = all.Where(s => s.Kind == pr.From)
                    .Select(s => (site: s, avail: res.Available(s.Id, pr.Product)))
                    .Where(s => s.avail >= Math.Min(VehicleConfig.TruckMinLoad, capacity))
                    .OrderByDescending(s => s.avail).ThenBy(s => s.site.Id).ToList();
                foreach (var source in sources)
                {
                    var pickup = Sites.AccessOf(network, source.site);
                    if (!pickup.HasValue || !routes.Exists(position, pickup.Value))
                    {
                        noRoute = true;
                        continue;
                    }
                    foreach (var drop in DropsFor(state, pr.Product, pr.From == SiteKind.hall, all, res))
                    {
                        var access = Sites.AccessOf(network, drop.Site);
                        if (!access.HasValue || !routes.Exists(pickup.Value, access.Value))
                        {
                            noRoute = true;
                            continue;
                        }
                        int quantity = Math.Min(capacity, Math.Min(source.avail, drop.Free));
                        if (quantity < 1) continue;
                        return new PlannedJob
                        {
                            Job = new Job { Product = pr.Product, FromId = source.site.Id, ToId = drop.Site.Id, Quantity = quantity },
                            Target = pickup.Value,
                        };
                    }
                }
            }
            return null;
        }
    }
}
