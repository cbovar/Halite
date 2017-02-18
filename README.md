## Halite C# bot and tools used to train this [bot](https://halite.io/user.php?userID=2036)


### Steps to train an [Halite](https://halite.io/) bot using [ConvNetSharp](https://github.com/cbovar/ConvNetSharp)

1) Get games historical data

* Download games historical using [HltDownloader](https://github.com/cbovar/Halite/tree/master/HltDownloader). Downloaded games will stored in 'games' folder. (e.g. games/2609/ for erdman)

2) Train a network
* Update downloaded games location [here](https://github.com/cbovar/Halite/blob/master/Training/FluentNetTraining.cs#L47)
* Update network structure [here](https://github.com/cbovar/Halite/blob/master/Training/FluentNetTraining.cs#L30). By default it is:

```c#
var convInputWith = 11; // Will extract 11x11 area

var net = FluentNet.Create(convInputWith, convInputWith, 3)
                    .Conv(3, 3, 16).Stride(2)
                    .Tanh()
                    .Conv(2, 2, 16)
                    .Tanh()
                    .FullyConn(100)
                    .Relu()
                    .FullyConn(5)
                    .Softmax(5).Build(); // 5 classes (1 for each direction)

```
* Update trainer algorithm [here](https://github.com/cbovar/Halite/blob/master/Training/FluentNetTraining.cs#L130). By default it is:
```c#
 var trainer = new AdamTrainer(singleNet) { BatchSize = 1024, LearningRate = 0.1, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };
```
* Run [Training](https://github.com/cbovar/Halite/tree/master/Training) project. Downloaded games will be loaded and training will start. Four files will be created:
  * Loss_single.csv 
  * Test_Accuracy_single.csv
  * Train_Accuracy_single.csv
  * net.dat (trained network)

## Run locally your trained bots

You can [ComparerRunner](https://github.com/cbovar/Halite/tree/master/CompareRunner) to make your bot fight each others
By default, version V28 and V45 (latest submission) will fight each other. You can add your newly trained bot by modifying the following list(https://github.com/cbovar/Halite/blob/master/CompareRunner/Program.cs#L15):
```c#
            // List of bots
            var bots = new List<Func<IPlayer>>
            {
                {() => new ThreeNetBot{Prefix="../../../networks/V45/", Name = "V45"} },
                {() => new SingleNetBot{Prefix="../../../networks/V28/", Name = "V28"} },
            };
```

In order to easily debug bots, Halite.cpp has been ported to C#. [Halite.cs](https://github.com/cbovar/Halite/blob/master/Runner.Core/Halite.cs)
