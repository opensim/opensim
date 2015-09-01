var updater = Class.create({
	initialize: function(divToUpdate, interval, file) {
		this.divToUpdate = divToUpdate;
		this.interval = interval;
		this.file = file;
		new PeriodicalExecuter(this.getUpdate.bind(this), this.interval);
	},
	
	getUpdate: function() {
		var oOptions = {
			method: "POST",
			parameters: "intervalPeriod="+this.interval,
			asynchronous: true,
			onComplete: function (oXHR, Json) {
				$(this.divToUpdate).innerHTML = oXHR.responseText;
			}
		};
		var oRequest = new Ajax.Updater(this.divToUpdate, this.file, oOptions);
	}
});