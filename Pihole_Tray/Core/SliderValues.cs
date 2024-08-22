using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    class SliderValues
    {

    public readonly Dictionary<int, Tuple<string, int>> Values = new Dictionary<int, Tuple<string, int>>
        {
            { 0, Tuple.Create("15 seconds", 15) },
            { 1, Tuple.Create("30 seconds", 30) },
            { 2, Tuple.Create("1 minute", 60) },
            { 3, Tuple.Create("2 minutes",60 * 2) },
            { 4, Tuple.Create("5 minutes",60 * 5) },
            { 5, Tuple.Create("10 minutes",60 * 10) },
            { 6, Tuple.Create("20 minutes",60 * 20) },
            { 7, Tuple.Create("30 minutes",60 * 30) },
            { 8, Tuple.Create("45 minutes",60 * 45) },
            { 9, Tuple.Create("1 hour",60 * 60) },
            { 10, Tuple.Create("2 hours",60 * 120) }
        };
    }


